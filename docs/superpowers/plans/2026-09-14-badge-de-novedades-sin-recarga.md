# Badge de novedades sin recargar — Plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para ejecutar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

**Objetivo:** que el contador de novedades se actualice solo, sin F5, en las
dos bandejas de la PWA del tenant y en las dos de la aplicación MVC de CAISY.

**Arquitectura:** sondeo periódico contra el endpoint de notificaciones, que
ya devuelve 304 con ETag. El servidor gana la cabecera `Cache-Control` que
hace efectiva esa revalidación. En la PWA el sondeo lo hace TanStack Query con
`refetchInterval`; en el MVC, un `setInterval` contra un endpoint nuevo que
devuelve solo el contador.

**Stack:** .NET 10, ASP.NET Core MVC, React 19 con TanStack Query, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-14-badge-de-novedades-sin-recarga-design.md`

## Restricciones globales

- Español correcto con acentos, UTF-8 sin BOM, sin mojibake. Nunca voseo.
- Prohibido `--no-verify` en commit y en push.
- Prohibido relajar una baseline, un umbral o una exclusión para que pase un
  gate. Si un gate falla, se arregla el contenido.
- `./verify.ps1` (o `./verify.sh`) antes de cada commit. **Docker debe estar
  corriendo**: los tests de integración usan Testcontainers.MsSql.
- Nunca afirmar que algo está verde sin haber ejecutado el comando y visto la
  salida.
- Rama `develop`, commits y push directos. No crear ramas.
- El MVC es deliberadamente online: **no** agregar service worker, caché
  offline ni IndexedDB.
- `Trajano.GestorCaisy` consume la API solo por HTTP. No agregar `DbContext`
  ni acceso SQL: hay una prueba de arquitectura que lo prohíbe.

## Hechos verificados antes de escribir este plan

No hace falta volver a comprobarlos:

- El endpoint de notificaciones ya calcula ETag y responde 304. Existe dos
  veces, una por agregado: `PedidosAlimentoEndpoints.MapNotificaciones`
  (línea 237) y `DespachosHuevoEndpoints.MapNotificacionesDespachoHuevo`
  (línea 140). Cada uno se monta en el grupo del tenant y en el de CAISY.
- El service worker de la PWA **no** intercepta `/api/`: `vite.config.ts:48`
  solo declara `globPatterns` de assets estáticos, sin `runtimeCaching`.
- `web/src/app/useConexion.ts` ya expone `useConexion(): boolean`, basado en
  `navigator.onLine` más los eventos `online`/`offline`.
- El badge ya existe en las cuatro pantallas y muestra el contador. Lo que
  falta es refrescarlo, no crearlo.
- `web/src/lib/http.ts:186` usa `fetch` con un `Request` estándar y sin
  `cache:` explícito, así que respeta el caché HTTP y reenvía `If-None-Match`
  cuando el servidor lo permite.

## Mapa de archivos

| Archivo | Qué cambia |
|---|---|
| `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs:237-253` | `Cache-Control: no-cache` en el sondeo |
| `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs:140-156` | Ídem |
| `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs` | Prueba de la cabecera |
| `web/src/lib/sondeo.ts` | **Crear:** la constante del intervalo |
| `web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx` | Sondeo y refresco de la bandeja |
| `web/src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx` | Prueba del sondeo |
| `web/src/features/despacho-huevo/DespachosHuevoPage.tsx` | Ídem |
| `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx` | Ídem |
| `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs` | Acción `ContadorNotificaciones` |
| `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs` | Ídem |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml:21` | Marcar el contador con un id |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml:21` | Ídem |
| `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/js/aplicacion.js` | Sondeo y pintado del badge |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs` | Prueba de la acción nueva |

---

### Task 1: El endpoint de sondeo declara `Cache-Control: no-cache`

**Archivos:**
- Modificar: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs:237-253`
- Modificar: `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs:140-156`
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consume: nada.
- Produce: la garantía de que la respuesta del sondeo lleva
  `Cache-Control: no-cache`, de la que dependen las tareas 2 y 3 para que el
  304 sirva de algo.

**Por qué:** `no-cache` no significa «no guardes», significa «guarda, pero
revalida siempre». Sin esa cabecera el navegador decide solo, y las dos
salidas posibles son malas: o sirve del caché sin preguntar y el badge nunca
cambia, o ignora el ETag y cada sondeo paga la respuesta completa.

La cabecera se pone **antes** del `if` del 304, para que la respuesta 304
también la lleve. Si solo se pusiera en la rama del 200, la primera
revalidación exitosa dejaría al navegador sin instrucción para la siguiente.

- [ ] **Paso 1: Escribir la prueba que falla**

Agregar al final de la clase `PedidosAlimentoEndpointsTests`, antes de la
llave de cierre:

```csharp
    // El sondeo del badge se apoya en el 304: sin no-cache, el navegador
    // decide solo si revalida, y el contador puede quedar congelado.
    [Fact]
    public async Task ElSondeoDeNotificacionesPideRevalidarSiempre()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);

        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/notificaciones", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.NoCache);
        var etag = respuesta.Headers.ETag?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(etag));

        // La respuesta 304 también instruye al navegador: si no la llevara,
        // la siguiente revalidación quedaría sin regla.
        var condicional = Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/notificaciones", tokenCliente);
        condicional.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var sinCambios = await cliente.SendAsync(condicional);
        Assert.Equal(HttpStatusCode.NotModified, sinCambios.StatusCode);
        Assert.True(sinCambios.Headers.CacheControl?.NoCache);
    }
```

- [ ] **Paso 2: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~ElSondeoDeNotificacionesPideRevalidarSiempre"
```

Esperado: FAIL, porque `CacheControl` viene nulo.

- [ ] **Paso 3: Agregar la cabecera en el endpoint de pedidos**

En `PedidosAlimentoEndpoints.cs`, dentro de `MapNotificaciones`, reemplazar:

```csharp
            var notificaciones = await mediator.Send(new ListarNotificacionesQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones, contador);
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
```

por:

```csharp
            var notificaciones = await mediator.Send(new ListarNotificacionesQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones, contador);
            // «no-cache» es «guarda, pero revalida siempre», no «no guardes».
            // Va antes del 304 para que esa respuesta también la lleve.
            contexto.Response.Headers.CacheControl = "no-cache";
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
```

- [ ] **Paso 4: Agregar la misma cabecera en el endpoint de despachos**

En `DespachosHuevoEndpoints.cs`, dentro de `MapNotificacionesDespachoHuevo`,
reemplazar:

```csharp
            var etag = CalcularEtag(notificaciones.Select(n => n.FechaUtc), contador);
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
```

por:

```csharp
            var etag = CalcularEtag(notificaciones.Select(n => n.FechaUtc), contador);
            // «no-cache» es «guarda, pero revalida siempre», no «no guardes».
            // Va antes del 304 para que esa respuesta también la lleve.
            contexto.Response.Headers.CacheControl = "no-cache";
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
```

- [ ] **Paso 5: Ejecutar y verificar que pasa**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~ElSondeoDeNotificaciones"
```

Esperado: PASS, incluida la prueba preexistente
`ElSondeoDeNotificacionesRespetaElEtagEnAmbosGrupos`.

- [ ] **Paso 6: Puerta de calidad y commit**

```
./verify.ps1
git add Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs
git commit -m "feat(api): el sondeo de notificaciones pide revalidar siempre"
```

---

### Task 2: La PWA sondea las novedades de pedidos de alimento

**Archivos:**
- Crear: `web/src/lib/sondeo.ts`
- Modificar: `web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx`
- Test: `web/src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx`

**Interfaces:**
- Consume: la cabecera de la Task 1.
- Produce: `INTERVALO_SONDEO_MS: number` exportado desde
  `web/src/lib/sondeo.ts`, que la Task 3 reutiliza.

**Dos trampas de esta tarea:**

1. **La invalidación puede entrar en bucle.** La bandeja usa
   `queryKey: ['pedidos-alimento', pagina, filtros]` y las novedades usan
   `['pedidos-alimento', 'notificaciones']`. Invalidar por el prefijo
   `['pedidos-alimento']` invalidaría también las novedades, que volverían a
   pedirse, que volverían a disparar la invalidación. Por eso se usa un
   `predicate` que solo alcanza las claves cuyo segundo elemento es un número,
   que son exactamente las de la bandeja.
2. **El contador solo no alcanza como señal.** Si el usuario marca una novedad
   como leída justo cuando llega otra, el contador queda igual y la lista no se
   refrescaría. La huella suma el contador, la cantidad de ítems y la fecha más
   reciente, y no depende del orden en que venga la lista.

- [ ] **Paso 1: Crear el módulo de la constante**

Crear `web/src/lib/sondeo.ts`:

```ts
// Cada cuánto se pregunta por novedades. El endpoint responde 304 cuando nada
// cambió, así que un sondeo sin noticias cuesta una respuesta vacía.
// Treinta segundos: una novedad de pedido o de despacho no es urgente al
// segundo, y media hora sería demasiado para un badge.
export const INTERVALO_SONDEO_MS = 30_000;
```

- [ ] **Paso 2: Escribir la prueba que falla**

Agregar al final del `describe` de `PedidosAlimentoPage.test.tsx`:

```tsx
  test('actualiza el contador de novedades sin recargar la página', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      vi.stubGlobal(
        'fetch',
        baseFetch({
          'GET /api/pedidos-alimento': respuesta(200, pagina([PEDIDO], 1)),
          'GET /api/pedidos-alimento/notificaciones': [
            respuesta(200, { items: [], contador: 0 }),
            respuesta(200, {
              items: [
                {
                  id: 'n1',
                  tipo: 'PedidoAceptado',
                  pedidoId: 'p1',
                  fechaUtc: '2026-09-14T10:00:00Z',
                  leida: false,
                  meta: null,
                },
              ],
              contador: 1,
            }),
          ],
        }),
      );
      renderPagina();
      expect(await screen.findByText(/Novedades de CAISY \(0\)/)).toBeInTheDocument();

      await vi.advanceTimersByTimeAsync(INTERVALO_SONDEO_MS + 100);

      await waitFor(() =>
        expect(screen.getByText(/Novedades de CAISY \(1\)/)).toBeInTheDocument(),
      );
    } finally {
      vi.useRealTimers();
    }
  });
```

Agregar el import de la constante arriba del archivo, junto a los demás:

```tsx
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
```

**Sobre los timers falsos:** `beforeEach(() => vi.restoreAllMocks())` **no**
restaura los timers. El `try/finally` con `vi.useRealTimers()` es obligatorio:
sin él, los timers falsos se filtran a las pruebas siguientes del archivo y las
rompen de formas difíciles de diagnosticar. `shouldAdvanceTime: true` es
necesario para que las promesas internas de TanStack Query sigan resolviendo.

- [ ] **Paso 3: Ejecutar y verificar que falla**

Ejecutar:

```
npm --prefix web test -- --run PedidosAlimentoPage
```

Esperado: FAIL. El contador sigue en 0 porque nadie vuelve a pedir las
novedades.

- [ ] **Paso 4: Implementar el sondeo**

En `PedidosAlimentoPage.tsx`, agregar a los imports existentes:

```tsx
import { useEffect, useRef, useState } from 'react';
import { useConexion } from '../../app/useConexion';
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
```

`useState` ya se importa desde `react`; si la línea ya existe, agregar
`useEffect` y `useRef` a esa misma importación en vez de duplicarla.

Reemplazar el bloque de la query de notificaciones (líneas 90-93):

```tsx
  const { data: notificaciones } = useQuery({
    queryKey: ['pedidos-alimento', 'notificaciones'],
    queryFn: listarNotificaciones,
  });
```

por:

```tsx
  const hayConexion = useConexion();

  const { data: notificaciones } = useQuery({
    queryKey: ['pedidos-alimento', 'notificaciones'],
    queryFn: listarNotificaciones,
    // Sin conexión no se sondea: la app es offline-first a propósito y un
    // reintento cada treinta segundos solo acumularía fallos.
    refetchInterval: hayConexion ? INTERVALO_SONDEO_MS : false,
    // Una pestaña oculta no necesita el badge al día.
    refetchIntervalInBackground: false,
  });

  // Huella de la bandeja de novedades. El contador solo no alcanza: marcar una
  // como leída mientras llega otra lo dejaría igual. No depende del orden en
  // que el servidor devuelva la lista.
  const huellaNovedades = notificaciones
    ? [
        notificaciones.contador,
        notificaciones.items.length,
        notificaciones.items.reduce((max, n) => (n.fechaUtc > max ? n.fechaUtc : max), ''),
      ].join(':')
    : null;
  const huellaPrevia = useRef<string | null>(null);

  useEffect(() => {
    if (huellaNovedades === null) return;
    if (huellaPrevia.current === null) {
      huellaPrevia.current = huellaNovedades; // primer render: nada que refrescar
      return;
    }
    if (huellaPrevia.current === huellaNovedades) return;
    huellaPrevia.current = huellaNovedades;
    // Solo la bandeja. Invalidar por el prefijo 'pedidos-alimento' alcanzaría
    // a esta misma query de novedades y el refresco se realimentaría sin fin;
    // la clave de la bandeja es la única cuyo segundo elemento es la página.
    queryClient.invalidateQueries({
      predicate: (query) =>
        query.queryKey[0] === 'pedidos-alimento' && typeof query.queryKey[1] === 'number',
    });
  }, [huellaNovedades, queryClient]);
```

- [ ] **Paso 5: Ejecutar y verificar que pasa**

Ejecutar:

```
npm --prefix web test -- --run PedidosAlimentoPage
```

Esperado: PASS en todo el archivo, no solo en la prueba nueva.

- [ ] **Paso 6: Puerta de calidad y commit**

```
./verify.ps1
git add web/src/lib/sondeo.ts web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx web/src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx
git commit -m "feat(web): la bandeja de pedidos actualiza las novedades sin recargar"
```

---

### Task 3: La PWA sondea las novedades de despachos de huevo

**Archivos:**
- Modificar: `web/src/features/despacho-huevo/DespachosHuevoPage.tsx`
- Test: `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx`

**Interfaces:**
- Consume: `INTERVALO_SONDEO_MS` de `web/src/lib/sondeo.ts` (Task 2).
- Produce: nada.

Misma forma que la Task 2, con las claves y los nombres de esta pantalla. Las
dos trampas descritas en la Task 2 aplican igual acá: la clave de la bandeja es
`['despachos-huevo', pagina, filtros]` y la de novedades
`['despachos-huevo', 'notificaciones']`.

- [ ] **Paso 1: Escribir la prueba que falla**

Agregar al final del `describe` de `DespachosHuevoPage.test.tsx`:

```tsx
  test('actualiza el contador de novedades sin recargar la página', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      vi.stubGlobal(
        'fetch',
        baseFetch({
          'GET /api/despachos-huevo': respuesta(200, pagina([DESPACHO], 1)),
          'GET /api/despachos-huevo/notificaciones': [
            respuesta(200, { items: [], contador: 0 }),
            respuesta(200, {
              items: [
                {
                  id: 'n1',
                  tipo: 'DespachoRecibido',
                  despachoHuevoId: 'h1',
                  fechaUtc: '2026-09-14T10:00:00Z',
                  leida: false,
                  meta: null,
                },
              ],
              contador: 1,
            }),
          ],
        }),
      );
      renderPagina();
      expect(await screen.findByText('D-000001')).toBeInTheDocument();

      await vi.advanceTimersByTimeAsync(INTERVALO_SONDEO_MS + 100);

      await waitFor(() =>
        expect(
          screen.getByText(/Novedades del despacho de huevo \(1\)/),
        ).toBeInTheDocument(),
      );
    } finally {
      vi.useRealTimers();
    }
  });
```

Agregar el import arriba del archivo:

```tsx
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
```

El texto del encabezado de esta pantalla es «Novedades del despacho de huevo»
(`DespachosHuevoPage.tsx:204`), distinto del «Novedades de CAISY» de la
pantalla de pedidos. No son intercambiables.

- [ ] **Paso 2: Ejecutar y verificar que falla**

Ejecutar:

```
npm --prefix web test -- --run DespachosHuevoPage
```

Esperado: FAIL. El contador no cambia.

- [ ] **Paso 3: Implementar el sondeo**

En `DespachosHuevoPage.tsx`, agregar a los imports:

```tsx
import { useEffect, useRef, useState } from 'react';
import { useConexion } from '../../app/useConexion';
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
```

Igual que en la Task 2: si `useState` ya viene de `react`, sumar `useEffect` y
`useRef` a esa importación en vez de duplicar la línea.

Reemplazar el bloque de la query de notificaciones (líneas 81-84):

```tsx
  const { data: notificaciones } = useQuery({
    queryKey: ['despachos-huevo', 'notificaciones'],
    queryFn: listarNotificacionesDespachoHuevo,
  });
```

por:

```tsx
  const hayConexion = useConexion();

  const { data: notificaciones } = useQuery({
    queryKey: ['despachos-huevo', 'notificaciones'],
    queryFn: listarNotificacionesDespachoHuevo,
    // Sin conexión no se sondea: la app es offline-first a propósito y un
    // reintento cada treinta segundos solo acumularía fallos.
    refetchInterval: hayConexion ? INTERVALO_SONDEO_MS : false,
    // Una pestaña oculta no necesita el badge al día.
    refetchIntervalInBackground: false,
  });

  // Huella de la bandeja de novedades. El contador solo no alcanza: marcar una
  // como leída mientras llega otra lo dejaría igual. No depende del orden en
  // que el servidor devuelva la lista.
  const huellaNovedades = notificaciones
    ? [
        notificaciones.contador,
        notificaciones.items.length,
        notificaciones.items.reduce((max, n) => (n.fechaUtc > max ? n.fechaUtc : max), ''),
      ].join(':')
    : null;
  const huellaPrevia = useRef<string | null>(null);

  useEffect(() => {
    if (huellaNovedades === null) return;
    if (huellaPrevia.current === null) {
      huellaPrevia.current = huellaNovedades; // primer render: nada que refrescar
      return;
    }
    if (huellaPrevia.current === huellaNovedades) return;
    huellaPrevia.current = huellaNovedades;
    // Solo la bandeja. Invalidar por el prefijo 'despachos-huevo' alcanzaría a
    // esta misma query de novedades y el refresco se realimentaría sin fin; la
    // clave de la bandeja es la única cuyo segundo elemento es la página.
    queryClient.invalidateQueries({
      predicate: (query) =>
        query.queryKey[0] === 'despachos-huevo' && typeof query.queryKey[1] === 'number',
    });
  }, [huellaNovedades, queryClient]);
```

- [ ] **Paso 4: Ejecutar y verificar que pasa**

Ejecutar:

```
npm --prefix web test -- --run DespachosHuevoPage
```

Esperado: PASS en todo el archivo.

- [ ] **Paso 5: Puerta de calidad y commit**

```
./verify.ps1
git add web/src/features/despacho-huevo/DespachosHuevoPage.tsx web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx
git commit -m "feat(web): la bandeja de despachos actualiza las novedades sin recargar"
```

---

### Task 4: El MVC de CAISY actualiza su badge sin recargar

**Archivos:**
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml:21`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml:21`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/js/aplicacion.js`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs`

**Interfaces:**
- Consume: la cabecera de la Task 1.
- Produce: la ruta `GET /Pedidos/Notificaciones/Contador` y
  `GET /RecepcionesHuevo/Notificaciones/Contador`, que devuelven
  `{ "contador": <int> }`.

El renderizado server-side no cambia: la página sigue pintando la lista de
novedades al cargar, y el sondeo solo actualiza el número del encabezado.

- [ ] **Paso 1: Escribir la prueba que falla**

Agregar al final de la clase `PedidosControllerTests`:

```csharp
    [Fact]
    public async Task ElContadorDeNotificacionesDevuelveSoloElNumero()
    {
        _api.NotificacionesDePedidos = new([], 7);

        var resultado = await _controlador.ContadorNotificaciones(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(resultado);
        var contador = json.Value!.GetType().GetProperty("contador")!.GetValue(json.Value);
        Assert.Equal(7, contador);
    }
```

La propiedad del doble de prueba se llama `NotificacionesDePedidos`
(`ApiIcarusFalsa.cs:268`) y arranca en `new([], 0)`. Por eso la prueba fija su
propio valor en vez de leerlo: un `Assert` contra el mismo campo que se acaba
de asignar no probaría nada. Los campos `_api` y `_controlador` ya existen en
la clase (`PedidosControllerTests.cs:17-18`).

- [ ] **Paso 2: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter "FullyQualifiedName~ElContadorDeNotificacionesDevuelveSoloElNumero"
```

Esperado: FAIL de compilación, porque `ContadorNotificaciones` no existe.

- [ ] **Paso 3: Agregar la acción en el controlador de pedidos**

En `PedidosController.cs`, agregar después de la acción `MarcarLeida`:

```csharp
    // Sondeo del badge: devuelve solo el número, sin volver a renderizar la
    // bandeja. La vista sigue pintando la lista al cargar.
    [HttpGet("Notificaciones/Contador")]
    public async Task<IActionResult> ContadorNotificaciones(CancellationToken token)
    {
        var notificaciones = await api.ListarNotificacionesPedidoAsync(token);
        return Json(new { contador = notificaciones.Contador });
    }
```

- [ ] **Paso 4: Agregar la acción equivalente en recepciones de huevo**

En `RecepcionesHuevoController.cs`, agregar la acción análoga después de su
acción de marcar como leída. La clase ya lleva `[Route("RecepcionesHuevo")]`
(línea 15), así que la ruta resultante es
`/RecepcionesHuevo/Notificaciones/Contador`:

```csharp
    // Sondeo del badge: devuelve solo el número, sin volver a renderizar la
    // bandeja. La vista sigue pintando la lista al cargar.
    [HttpGet("Notificaciones/Contador")]
    public async Task<IActionResult> ContadorNotificaciones(CancellationToken token)
    {
        var notificaciones = await api.ListarNotificacionesDespachoHuevoAsync(token);
        return Json(new { contador = notificaciones.Contador });
    }
```

- [ ] **Paso 5: Marcar el contador en las dos vistas**

En `Views/Pedidos/Index.cshtml`, reemplazar la línea 21:

```cshtml
    <h2>Novedades para CAISY (@Model.Notificaciones.Contador sin leer)</h2>
```

por:

```cshtml
    <h2>Novedades para CAISY (<span data-contador-novedades
        data-url-contador="@Url.Action("ContadorNotificaciones")">@Model.Notificaciones.Contador</span> sin leer)</h2>
```

En `Views/RecepcionesHuevo/Index.cshtml`, aplicar exactamente el mismo cambio
sobre su línea 21, que tiene el mismo texto.

El JS toma la URL del atributo en vez de llevarla escrita: así cada vista
apunta a su propio controlador y el script no conoce rutas.

- [ ] **Paso 6: Agregar el sondeo en `aplicacion.js`**

En `wwwroot/js/aplicacion.js`, agregar dentro de la IIFE, justo antes del
`})();` final:

```javascript
    /* Sondeo del badge de novedades. El endpoint responde 304 cuando nada
       cambió, así que un sondeo sin noticias cuesta una respuesta vacía.
       Treinta segundos, igual que la PWA. */
    var contadorNovedades = document.querySelector('[data-contador-novedades]');
    if (contadorNovedades) {
        var urlContador = contadorNovedades.getAttribute('data-url-contador');
        window.setInterval(function () {
            /* Una pestaña oculta no necesita el badge al día. */
            if (document.hidden) return;
            window.fetch(urlContador, { credentials: 'same-origin' })
                .then(function (respuesta) {
                    return respuesta.ok ? respuesta.json() : null;
                })
                .then(function (datos) {
                    if (!datos) return;
                    contadorNovedades.textContent = String(datos.contador);
                })
                .catch(function () {
                    /* Un sondeo fallido no molesta al usuario: el siguiente
                       lo vuelve a intentar. */
                });
        }, 30000);
    }
```

- [ ] **Paso 7: Ejecutar las pruebas y verificar que pasan**

Ejecutar:

```
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests
```

Esperado: PASS en toda la suite, no solo en la prueba nueva.

- [ ] **Paso 8: Puerta de calidad, commit y push**

```
./verify.ps1
git add Icarus/src/Apps/Trajano.GestorCaisy/ Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs
git commit -m "feat(caisy): el badge de novedades se actualiza sin recargar la pagina"
git push
```

---

## Verificación manual

Con `./iniciar-pc1.ps1` (sin `-RecrearDatos`: este plan no toca el esquema).
Contraseña de la semilla en pc1: `Admin123!`.

Hace falta tener dos sesiones abiertas a la vez, en dos navegadores o en una
ventana de incógnito, porque el punto es ver el cambio sin tocar la pantalla.

1. Abrir la bandeja de pedidos de la PWA como `cliente@icarus.test` y anotar
   el contador de novedades. **No tocar la pestaña.**
2. Desde la otra sesión, como gestor con `GestorPedidoAlimento`, aceptar un
   pedido de ese cliente.
3. Volver a mirar la pantalla del cliente: en menos de un minuto el contador
   sube y el pedido aparece con su estado nuevo, sin haber presionado F5.
4. Repetir el ejercicio al revés: el cliente envía un pedido y el badge del
   gestor en el MVC sube solo.
5. Repetir los pasos 1 a 4 con despachos de huevo y un gestor con
   `GestorRecepcionHuevos`.
6. Con las herramientas de desarrollo abiertas en la pestaña de red, confirmar
   que los sondeos repetidos responden **304** y no 200. Si responden 200 con
   cuerpo completo, la cabecera de la Task 1 no está llegando al navegador.
7. Cortar la conexión de red con la PWA abierta: los sondeos deben detenerse,
   no acumular errores en la consola. Al restablecerla, deben reanudarse solos.
