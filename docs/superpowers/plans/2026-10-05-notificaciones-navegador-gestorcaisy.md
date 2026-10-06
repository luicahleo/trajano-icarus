# Notificaciones del navegador en Trajano.GestorCaisy — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar una campanita global (header, badge + desplegable) y un aviso nativo del navegador (Web Notifications API) para pedidos de alimento y recepción de huevo en `Trajano.GestorCaisy`, siguiendo el spec aprobado en `docs/superpowers/specs/2026-10-05-notificaciones-navegador-gestorcaisy-design.md`.

**Architecture:** Todo el trabajo vive dentro de `Trajano.GestorCaisy` (MVC server-rendered). Se reusan los dos endpoints de contador que ya existen (`Pedidos/Notificaciones/Contador`, `RecepcionesHuevo/Notificaciones/Contador`), enriqueciendo su JSON de forma aditiva. La campanita es el canal confiable (no depende de ningún permiso); el aviso del navegador es una mejora encima, gateada por un banner de opt-in. Un único sondeo de 30 s en `aplicacion.js` alimenta ambos canales. No se toca `Icarus.Host` ni el PWA (`web/`).

**Tech Stack:** ASP.NET Core MVC (Razor), JavaScript vanilla sin dependencias (mismo estilo que `wwwroot/js/aplicacion.js`), xUnit para las pruebas de C#.

## Global Constraints

- Español neutro sin voseo en todo texto de interfaz, comentarios y mensajes de commit.
- UTF-8 sin BOM, sin mojibake, con acentos correctos.
- Anti-PII: el texto de la `Notification` del navegador y de la campanita nunca incluye precios, montos ni datos nominales — solo el texto genérico ya traducido por `EtiquetaNotificacion`/`EtiquetaChip` (nombres de tipo de evento, nunca contenido de negocio).
- `./verify.ps1` (o `./verify.sh`) obligatorio antes de cada commit y push; prohibido `--no-verify`.
- TDD estricto donde aplica (endpoints enriquecidos, markup gateado); la Task 1 es un refactor de movimiento de código sin cambio de comportamiento — su verificación es la suite existente en verde antes y después, no un test nuevo que deba fallar primero.
- Rama `develop`, sin pull requests; `git push` solo tras confirmación explícita del usuario.
- No se crea ningún Service Worker ni se toca `web/` ni `Icarus.Host`: todo el trabajo es MVC + JS vanilla dentro de `Trajano.GestorCaisy`.
- No se agrega "marcar como leída" desde la campanita ni desde la `Notification`: esa acción sigue viviendo solo en `/Pedidos` y `/RecepcionesHuevo`.
- No se reintroduce el tipo `CreditoInsuficiente` ni ningún tipo nuevo de notificación de huevo.

## File Structure

- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs` — agrega `EtiquetasNotificacionPedido` (público).
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/RecepcionesHuevoVistas.cs` — agrega `EtiquetasNotificacionDespachoHuevo` (público).
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml` — usa el helper movido.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml` — usa el helper movido.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs` — enriquece `ContadorNotificaciones`.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs` — enriquece `ContadorNotificaciones`.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css` — clases de campanita y banner.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml` — campanita + banner.
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/js/aplicacion.js` — sondeo global, campanita, permiso y `Notification`.
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs` — prueba del contrato enriquecido.
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/RecepcionesHuevoControllerTests.cs` — pruebas del contrato enriquecido.
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs` — pruebas de gating de la campanita/banner.

No se crean archivos nuevos: se reutiliza la infraestructura existente (modelos de vista, controladores, `_Layout.cshtml`, `aplicacion.js`, `estilos.css`).

---

### Task 1: Extraer los helpers de texto de notificación a los modelos

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/RecepcionesHuevoVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml`

**Interfaces:**
- Produces: `EtiquetasNotificacionPedido.Texto(string tipo)`, `EtiquetasNotificacionPedido.Chip(string tipo)`, `EtiquetasNotificacionDespachoHuevo.Texto(string tipo)`, `EtiquetasNotificacionDespachoHuevo.Chip(string tipo)` — todos públicos, estáticos, en el namespace `Trajano.GestorCaisy.Models` (ya importado globalmente por `_ViewImports.cshtml`). Los usa la Task 2 y la Task 3 desde los controladores.

Este es un refactor de movimiento de código: el texto no cambia, solo el lugar donde vive (de `@functions` privado en la vista a una clase pública en el modelo), para que el controlador también pueda usarlo.

- [ ] **Step 1: Confirmar la base verde antes de mover código**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter "FullyQualifiedName~FlujoPedidosTests|FullyQualifiedName~FlujoRecepcionesHuevoTests"`
Expected: PASS (estas pruebas de integración ya verifican que la bandeja de novedades muestra el texto traducido; son la red de seguridad de este refactor).

- [ ] **Step 2: Mover los helpers de Pedidos**

En `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml`, borra el bloque `@functions` completo:

```cshtml
@functions {
    private static string EtiquetaNotificacion(string tipo) => tipo switch
    {
        "PedidoSolicitado" => "Pedido solicitado",
        "PedidoReenviado" => "Pedido reenviado",
        "PedidoDevuelto" => "Devolución",
        "PedidoRechazado" => "Rechazo",
        "PedidoAceptado" => "Aceptación",
        "EntregaEstimadaActualizada" => "Entrega estimada",
        _ => tipo,
    };

    private static string EtiquetaChip(string tipo) => tipo switch
    {
        "PedidoSolicitado" or "PedidoReenviado" => "solicitado",
        "PedidoDevuelto" => "borrador",
        "PedidoRechazado" => "rechazado",
        "PedidoAceptado" or "EntregaEstimadaActualizada" => "aceptado",
        _ => "borrador",
    };
}
```

y en la misma vista cambia la línea que los usa:

```cshtml
<span class="chip chip--@EtiquetaChip(notificacion.Tipo)">@EtiquetaNotificacion(notificacion.Tipo)</span>
```

por:

```cshtml
<span class="chip chip--@EtiquetasNotificacionPedido.Chip(notificacion.Tipo)">@EtiquetasNotificacionPedido.Texto(notificacion.Tipo)</span>
```

En `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs`, agrega al final del archivo (mismo texto, ahora público):

```csharp
// Traduce el tipo de notificación de pedido a texto legible y a la clase de
// chip visual; lo usa tanto la vista (bandeja inline) como el endpoint JSON
// del sondeo global de la campanita (spec 2026-10-05).
public static class EtiquetasNotificacionPedido
{
    public static string Texto(string tipo) => tipo switch
    {
        "PedidoSolicitado" => "Pedido solicitado",
        "PedidoReenviado" => "Pedido reenviado",
        "PedidoDevuelto" => "Devolución",
        "PedidoRechazado" => "Rechazo",
        "PedidoAceptado" => "Aceptación",
        "EntregaEstimadaActualizada" => "Entrega estimada",
        _ => tipo,
    };

    public static string Chip(string tipo) => tipo switch
    {
        "PedidoSolicitado" or "PedidoReenviado" => "solicitado",
        "PedidoDevuelto" => "borrador",
        "PedidoRechazado" => "rechazado",
        "PedidoAceptado" or "EntregaEstimadaActualizada" => "aceptado",
        _ => "borrador",
    };
}
```

- [ ] **Step 3: Mover los helpers de RecepcionesHuevo**

En `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml`, el bloque `@functions` tiene tres métodos; borra solo los dos de notificación (`EtiquetaNotificacion`, `EtiquetaChip`) y deja intacto `ClaseChipEstado` (no es parte de este refactor):

```cshtml
@functions {
    // Esta bandeja es global de CAISY (ClienteId nulo en la consulta). El
    // único tipo que se creaba con ese alcance era CreditoInsuficiente, y la
    // corrección 2026-09-14 dejó de emitirlo: no se manda ninguna alerta por
    // saldo. Las filas ya existentes en base se conservan y caen en la rama
    // por defecto. El valor 1 del enum queda reservado y no se renumera.
    private static string EtiquetaNotificacion(string tipo) => tipo;

    private static string EtiquetaChip(string tipo) => tipo switch
    {
        _ => "borrador",
    };

    private static string ClaseChipEstado(string estado) => estado switch
    {
        "Despachado" => "solicitado",
        "Recibido" => "aceptado",
        _ => "borrador",
    };
}
```

queda:

```cshtml
@functions {
    private static string ClaseChipEstado(string estado) => estado switch
    {
        "Despachado" => "solicitado",
        "Recibido" => "aceptado",
        _ => "borrador",
    };
}
```

y la línea que usaba los dos métodos borrados:

```cshtml
<span class="chip chip--@EtiquetaChip(notificacion.Tipo)">@EtiquetaNotificacion(notificacion.Tipo)</span>
```

pasa a:

```cshtml
<span class="chip chip--@EtiquetasNotificacionDespachoHuevo.Chip(notificacion.Tipo)">@EtiquetasNotificacionDespachoHuevo.Texto(notificacion.Tipo)</span>
```

En `Icarus/src/Apps/Trajano.GestorCaisy/Models/RecepcionesHuevoVistas.cs`, agrega al final del archivo:

```csharp
// Esta bandeja es global de CAISY (ClienteId nulo en la consulta). El único
// tipo que se creaba con ese alcance era CreditoInsuficiente, y la corrección
// 2026-09-14 dejó de emitirlo: no se manda ninguna alerta por saldo. Las
// filas ya existentes en base se conservan y caen en la rama por defecto. El
// valor 1 del enum queda reservado y no se renumera. Lo usa tanto la vista
// como el endpoint JSON del sondeo global de la campanita (spec 2026-10-05).
public static class EtiquetasNotificacionDespachoHuevo
{
    public static string Texto(string tipo) => tipo;

    public static string Chip(string tipo) => tipo switch
    {
        _ => "borrador",
    };
}
```

- [ ] **Step 4: Confirmar que la suite sigue en verde tras el movimiento**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter "FullyQualifiedName~FlujoPedidosTests|FullyQualifiedName~FlujoRecepcionesHuevoTests"`
Expected: PASS (mismo resultado que el Step 1: el texto renderizado no cambió)

- [ ] **Step 5: Ejecutar toda la suite de `Trajano.GestorCaisy.Tests` por si algo más referenciaba los métodos privados**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs Icarus/src/Apps/Trajano.GestorCaisy/Models/RecepcionesHuevoVistas.cs Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Index.cshtml Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml
git commit -m "refactor(gestor-caisy): expone las etiquetas de notificacion como helpers publicos"
```

---

### Task 2: Enriquecer el contador de notificaciones de Pedidos con items traducidos

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs`

**Interfaces:**
- Consumes: `EtiquetasNotificacionPedido.Texto`/`.Chip` (Task 1).
- Produces: `GET Pedidos/Notificaciones/Contador` devuelve `{ contador, items: [{ id, mensaje, chip, fechaUtc, pedidoId }] }` (antes solo `{ contador }`); `items` ordenado por `fechaUtc` descendente, tope de 5.

- [ ] **Step 1: Escribir la prueba que falla**

Agrega a `PedidosControllerTests.cs`, junto a `ElContadorDeNotificacionesDevuelveSoloElNumero`:

```csharp
    [Fact]
    public async Task ElContadorDeNotificacionesIncluyeLosItemsTraducidosOrdenadosYLimitados()
    {
        var antigua = Guid.NewGuid();
        var reciente = Guid.NewGuid();
        var pedidoAntiguo = Guid.NewGuid();
        var pedidoReciente = Guid.NewGuid();
        _api.NotificacionesDePedidos = new(
            [
                new NotificacionPedidoApi(
                    antigua, "PedidoSolicitado", pedidoAntiguo,
                    new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), false, null),
                new NotificacionPedidoApi(
                    reciente, "PedidoDevuelto", pedidoReciente,
                    new DateTime(2026, 10, 5, 14, 30, 0, DateTimeKind.Utc), false, null),
            ],
            2);

        var resultado = await _controlador.ContadorNotificaciones(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(resultado);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            json.Value!.GetType().GetProperty("items")!.GetValue(json.Value));
        var lista = items.Cast<object>().ToList();
        Assert.Equal(2, lista.Count);
        var primero = lista[0].GetType();
        Assert.Equal(reciente, primero.GetProperty("id")!.GetValue(lista[0]));
        Assert.Equal("Devolución", primero.GetProperty("mensaje")!.GetValue(lista[0]));
        Assert.Equal("borrador", primero.GetProperty("chip")!.GetValue(lista[0]));
        Assert.Equal(pedidoReciente, primero.GetProperty("pedidoId")!.GetValue(lista[0]));
        var segundo = lista[1].GetType();
        Assert.Equal(antigua, segundo.GetProperty("id")!.GetValue(lista[1]));
    }
```

Agrega `using System.Linq;` al principio del archivo si todavía no está.

- [ ] **Step 2: Ejecutar y confirmar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~ElContadorDeNotificacionesIncluyeLosItemsTraducidosOrdenadosYLimitados`
Expected: FAIL (`GetProperty("items")` devuelve `null`: el JSON actual solo tiene `contador`)

- [ ] **Step 3: Enriquecer el endpoint**

En `PedidosController.cs`, reemplaza:

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

por:

```csharp
    // Sondeo del badge por página (sin cambios de contrato: sigue leyendo
    // .contador) y de la campanita global (spec 2026-10-05, lee además
    // .items). Los primeros 5 por fecha bastan para el desplegable; la
    // bandeja completa de la página sigue siendo la fuente de verdad.
    [HttpGet("Notificaciones/Contador")]
    public async Task<IActionResult> ContadorNotificaciones(CancellationToken token)
    {
        var notificaciones = await api.ListarNotificacionesPedidoAsync(token);
        return Json(new
        {
            contador = notificaciones.Contador,
            items = notificaciones.Items
                .OrderByDescending(n => n.FechaUtc)
                .Take(5)
                .Select(n => new
                {
                    id = n.Id,
                    mensaje = EtiquetasNotificacionPedido.Texto(n.Tipo),
                    chip = EtiquetasNotificacionPedido.Chip(n.Tipo),
                    fechaUtc = n.FechaUtc,
                    pedidoId = n.PedidoId,
                }),
        });
    }
```

Agrega `using System.Linq;` al principio de `PedidosController.cs` si todavía no está.

- [ ] **Step 4: Ejecutar y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter "FullyQualifiedName~PedidosControllerTests"`
Expected: PASS (incluida `ElContadorDeNotificacionesDevuelveSoloElNumero`, que sigue leyendo `.contador` sin cambios)

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs
git commit -m "feat(gestor-caisy): el contador de notificaciones de pedidos incluye items traducidos"
```

---

### Task 3: Enriquecer el contador de notificaciones de RecepcionesHuevo con items traducidos

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/RecepcionesHuevoControllerTests.cs`

**Interfaces:**
- Consumes: `EtiquetasNotificacionDespachoHuevo.Texto`/`.Chip` (Task 1).
- Produces: `GET RecepcionesHuevo/Notificaciones/Contador` devuelve `{ contador, items: [{ id, mensaje, chip, fechaUtc, despachoHuevoId }] }`.

- [ ] **Step 1: Escribir la prueba que falla**

Agrega a `RecepcionesHuevoControllerTests.cs`:

```csharp
    [Fact]
    public async Task ElContadorDeNotificacionesIncluyeLosItemsTraducidosOrdenadosYLimitados()
    {
        var antigua = Guid.NewGuid();
        var reciente = Guid.NewGuid();
        var despachoAntiguo = Guid.NewGuid();
        var despachoReciente = Guid.NewGuid();
        _api.NotificacionesDeDespachosHuevo = new(
            [
                new NotificacionDespachoHuevoApi(
                    antigua, "AlgunTipo", despachoAntiguo,
                    new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), false, null),
                new NotificacionDespachoHuevoApi(
                    reciente, "OtroTipo", despachoReciente,
                    new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc), false, null),
            ],
            2);

        var resultado = await _controlador.ContadorNotificaciones(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(resultado);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            json.Value!.GetType().GetProperty("items")!.GetValue(json.Value));
        var lista = items.Cast<object>().ToList();
        Assert.Equal(2, lista.Count);
        var primero = lista[0].GetType();
        Assert.Equal(reciente, primero.GetProperty("id")!.GetValue(lista[0]));
        // EtiquetasNotificacionDespachoHuevo.Texto es un stub (tipo => tipo):
        // no hay traduccion real hasta que exista un tipo activo para esta bandeja.
        Assert.Equal("OtroTipo", primero.GetProperty("mensaje")!.GetValue(lista[0]));
        Assert.Equal("borrador", primero.GetProperty("chip")!.GetValue(lista[0]));
        Assert.Equal(despachoReciente, primero.GetProperty("despachoHuevoId")!.GetValue(lista[0]));
    }
```

Agrega `using System;` y `using System.Linq;` al principio del archivo si todavía no están (ya usa `Guid`/`DateTime` en otras pruebas; confirma antes de duplicar el using).

- [ ] **Step 2: Ejecutar y confirmar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~RecepcionesHuevoControllerTests`
Expected: FAIL (`GetProperty("items")` devuelve `null`)

- [ ] **Step 3: Enriquecer el endpoint**

En `RecepcionesHuevoController.cs`, reemplaza:

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

por:

```csharp
    // Sondeo del badge por página (sin cambios de contrato: sigue leyendo
    // .contador) y de la campanita global (spec 2026-10-05, lee además
    // .items).
    [HttpGet("Notificaciones/Contador")]
    public async Task<IActionResult> ContadorNotificaciones(CancellationToken token)
    {
        var notificaciones = await api.ListarNotificacionesDespachoHuevoAsync(token);
        return Json(new
        {
            contador = notificaciones.Contador,
            items = notificaciones.Items
                .OrderByDescending(n => n.FechaUtc)
                .Take(5)
                .Select(n => new
                {
                    id = n.Id,
                    mensaje = EtiquetasNotificacionDespachoHuevo.Texto(n.Tipo),
                    chip = EtiquetasNotificacionDespachoHuevo.Chip(n.Tipo),
                    fechaUtc = n.FechaUtc,
                    despachoHuevoId = n.DespachoHuevoId,
                }),
        });
    }
```

Agrega `using System.Linq;` al principio de `RecepcionesHuevoController.cs` si todavía no está.

- [ ] **Step 4: Ejecutar y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~RecepcionesHuevoControllerTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/RecepcionesHuevoControllerTests.cs
git commit -m "feat(gestor-caisy): el contador de notificaciones de recepciones incluye items traducidos"
```

---

### Task 4: CSS de la campanita y el banner de permiso

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css`

**Interfaces:** ninguna; solo clases nuevas, sin tocar las existentes.

No hay test automatizado para CSS puro en este proyecto; este paso se verifica visualmente en la Task 7.

- [ ] **Step 1: Agregar las clases nuevas**

Al final de `estilos.css` (después del bloque de `.fila--advertencia`/`.advertencia-precio`), agrega:

```css
/* Campanita global de novedades (spec 2026-10-05): badge + desplegable en el
   header, visible en toda la aplicación. No depende de ningún permiso. */
.campana { position: relative; }

.campana__boton {
    position: relative;
    background: transparent;
    border: none;
    color: #FFFFFF;
    font-size: 1.25rem;
    line-height: 1;
    padding: 4px 8px;
    cursor: pointer;
}

.campana__badge {
    position: absolute;
    top: -2px;
    right: -2px;
    min-width: 16px;
    padding: 1px 5px;
    background-color: var(--terracota);
    color: #FFFFFF;
    border-radius: 999px;
    font-size: 0.6875rem;
    font-weight: 700;
    line-height: 1.3;
}

.campana__menu {
    position: absolute;
    top: 100%;
    right: 0;
    z-index: 20;
    margin-top: 6px;
    min-width: 280px;
    max-width: 360px;
    max-height: 320px;
    overflow-y: auto;
    background-color: var(--papel);
    color: var(--grafito);
    border: 1px solid var(--borde);
    border-radius: var(--radio-tarjeta);
    box-shadow: 0 8px 24px rgba(18, 38, 42, 0.18);
    padding: 8px;
}

.campana__menu .novedades { text-align: left; }

/* Banner de opt-in para el permiso de Notification (spec 2026-10-05): solo se
   muestra si Notification.permission sigue en "default"; lo controla
   aplicacion.js, nunca CSS puro. */
.banner-notificaciones {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    margin: 12px 20px 0;
}
```

- [ ] **Step 2: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css
git commit -m "style(gestor-caisy): clases de la campanita global y el banner de notificaciones"
```

---

### Task 5: Markup de la campanita y el banner en `_Layout.cshtml`

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs`

**Interfaces:**
- Consumes: `ReclamosCaisy.TieneGestorPedidoAlimento`/`TieneGestorRecepcionHuevos` (ya existen); `Url.Action` hacia las acciones `ContadorNotificaciones`/`Index` de `PedidosController`/`RecepcionesHuevoController` (ya existen).
- Produces: markup con atributos `data-campana`, `data-campana-boton`, `data-campana-badge`, `data-campana-menu`, `data-fuente-novedades`, `data-pagina`, `data-dominio`, `data-banner-notificaciones`, `data-banner-activar`, que consume la Task 6.

- [ ] **Step 1: Escribir las pruebas que fallan**

Agrega a `LayoutTests.cs`:

```csharp
    [Fact]
    public async Task LaCampanitaYElBannerAparecenParaUnGestorConAmbasFunciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 3);

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("data-campana", html);
        Assert.Contains("data-fuente-novedades=\"/Pedidos/Notificaciones/Contador\"", html);
        Assert.Contains("data-fuente-novedades=\"/RecepcionesHuevo/Notificaciones/Contador\"", html);
        Assert.Contains("data-banner-notificaciones", html);
        Assert.Contains("Activar notificaciones", html);
    }

    [Fact]
    public async Task LaCampanitaNoAparecePorFueraDeLasFuncionesDeCaisy()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(rol: "Administrador", funcCaisy: null);

        var html = await cliente.GetStringAsync("/Precios");

        Assert.DoesNotContain("data-campana", html);
        Assert.DoesNotContain("data-banner-notificaciones", html);
    }
```

Revisa antes de escribir estas pruebas si `AplicacionDePruebas.AccederAsync` ya soporta `rol: "Administrador"` llegando a `/Precios` sin 403/redirect inesperado; si la política de `/Precios` exige `GestorCaisy`, usa en su lugar un acceso con `rol: "GestorCaisy", funcCaisy: 0` (ninguna funcionalidad) para la segunda prueba, que sí llega a `/Precios` por rol pero sin funciones.

- [ ] **Step 2: Ejecutar y confirmar que fallan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~LayoutTests`
Expected: FAIL en las dos pruebas nuevas (el markup todavía no existe)

- [ ] **Step 3: Agregar la campanita dentro de `<header class="barra">`**

En `_Layout.cshtml`, reemplaza:

```cshtml
        @if (User.Identity?.IsAuthenticated == true)
        {
            @if (User.FindFirst(ConstantesAutorizacion.ClaimCorreo)?.Value is { } correo)
            {
                <span class="barra__cuenta" title="@correo">@correo</span>
            }
            <form class="barra__salir" asp-controller="Sesion" asp-action="Salir" method="post">
                <button type="submit" class="boton boton--claro">Cerrar sesión</button>
            </form>
        }
```

por:

```cshtml
        @if (User.Identity?.IsAuthenticated == true)
        {
            @if (User.FindFirst(ConstantesAutorizacion.ClaimCorreo)?.Value is { } correo)
            {
                <span class="barra__cuenta" title="@correo">@correo</span>
            }
            @if (ReclamosCaisy.TieneGestorPedidoAlimento(User) || ReclamosCaisy.TieneGestorRecepcionHuevos(User))
            {
                <div class="campana" data-campana>
                    <button type="button" class="campana__boton" data-campana-boton aria-label="Notificaciones">
                        🔔<span class="campana__badge" data-campana-badge hidden>0</span>
                    </button>
                    <div class="campana__menu" data-campana-menu hidden></div>
                    @if (ReclamosCaisy.TieneGestorPedidoAlimento(User))
                    {
                        <span hidden data-fuente-novedades="@Url.Action("ContadorNotificaciones", "Pedidos")"
                              data-pagina="@Url.Action("Index", "Pedidos")" data-dominio="Pedidos de alimento"></span>
                    }
                    @if (ReclamosCaisy.TieneGestorRecepcionHuevos(User))
                    {
                        <span hidden data-fuente-novedades="@Url.Action("ContadorNotificaciones", "RecepcionesHuevo")"
                              data-pagina="@Url.Action("Index", "RecepcionesHuevo")" data-dominio="Recepción de huevo"></span>
                    }
                </div>
            }
            <form class="barra__salir" asp-controller="Sesion" asp-action="Salir" method="post">
                <button type="submit" class="boton boton--claro">Cerrar sesión</button>
            </form>
        }
```

- [ ] **Step 4: Agregar el banner de permiso, justo después de `</header>`**

Inmediatamente después de la línea `</header>` y antes de `<div class="cuerpo">`, agrega:

```cshtml
    @if (User.Identity?.IsAuthenticated == true
        && (ReclamosCaisy.TieneGestorPedidoAlimento(User) || ReclamosCaisy.TieneGestorRecepcionHuevos(User)))
    {
        <div class="alerta alerta--aviso banner-notificaciones" role="note" data-banner-notificaciones hidden>
            <p>Activa los avisos del navegador para enterarte de nuevos pedidos y recepciones sin tener que revisar la página.</p>
            <button type="button" class="boton boton--neutro" data-banner-activar>Activar notificaciones</button>
        </div>
    }
```

- [ ] **Step 5: Ejecutar y confirmar que las pruebas nuevas pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~LayoutTests`
Expected: PASS (las 5 pruebas de `LayoutTests.cs`, incluidas las 3 ya existentes)

- [ ] **Step 6: Ejecutar toda la suite de `Trajano.GestorCaisy.Tests` por si algún test de otra página cuenta nodos del header de forma frágil**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs
git commit -m "feat(gestor-caisy): agrega la campanita global y el banner de notificaciones al layout"
```

---

### Task 6: Sondeo global, campanita y permiso en `aplicacion.js`

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/js/aplicacion.js`

**Interfaces:**
- Consumes: los atributos `data-*` de la Task 5 y el JSON enriquecido de las Tasks 2 y 3.
- Produces: comportamiento en navegador; sin interfaz de código para otras tareas.

El proyecto no tiene arnés de pruebas para JS puro (confirmado en la Task de investigación previa a este plan). Este paso no tiene test automatizado — se verifica manualmente en la Task 7. Escribe el código con cuidado y revísalo dos veces antes de continuar.

- [ ] **Step 1: Agregar el bloque de la campanita y el sondeo global**

Dentro de la función autoejecutada de `aplicacion.js` (`(function () { 'use strict'; ... })();`), después del bloque existente del "Sondeo del badge de novedades" (el que termina en `}, 30000); }`) y antes del cierre `})();`, agrega:

```javascript
    /* Campanita global + aviso del navegador (spec 2026-10-05). Un solo
       sondeo cada 30 s alimenta la campanita (siempre visible, no depende de
       ningún permiso) y, si Notification.permission ya es "granted", dispara
       además un aviso nativo. El correo de la cuenta viaja en el atributo
       title que _Layout.cshtml ya pone en .barra__cuenta: se reusa en vez de
       agregar un atributo nuevo. */
    var campana = document.querySelector('[data-campana]');
    if (campana) {
        var cuentaEl = document.querySelector('.barra__cuenta');
        var correo = cuentaEl ? cuentaEl.getAttribute('title') || '' : '';
        var fuentes = Array.prototype.slice.call(
            campana.querySelectorAll('[data-fuente-novedades]'));
        var botonCampana = campana.querySelector('[data-campana-boton]');
        var badge = campana.querySelector('[data-campana-badge]');
        var menu = campana.querySelector('[data-campana-menu]');

        var claveVisto = function (urlContador) {
            return 'campana-visto:' + correo + ':' + urlContador;
        };

        var ultimoVisto = function (urlContador) {
            var valor = window.localStorage.getItem(claveVisto(urlContador));
            return valor === null ? null : parseInt(valor, 10);
        };

        var guardarVisto = function (urlContador, contador) {
            window.localStorage.setItem(claveVisto(urlContador), String(contador));
        };

        var escaparHtml = function (texto) {
            var div = document.createElement('div');
            div.textContent = texto;
            return div.innerHTML;
        };

        var renderizarItem = function (item) {
            return '<li><a class="enlace" href="' + item.urlPagina + '">'
                + '<span class="chip chip--' + escaparHtml(item.chip) + '">'
                + escaparHtml(item.mensaje) + '</span> '
                + new Date(item.fechaUtc).toLocaleString('es-BO')
                + '</a></li>';
        };

        if (botonCampana && menu) {
            botonCampana.addEventListener('click', function () {
                if (menu.hasAttribute('hidden')) menu.removeAttribute('hidden');
                else menu.setAttribute('hidden', '');
            });
        }

        var sondearCampana = function () {
            if (document.hidden) return;
            var pendientes = fuentes.map(function (fuente) {
                var urlContador = fuente.getAttribute('data-fuente-novedades');
                var urlPagina = fuente.getAttribute('data-pagina');
                var dominio = fuente.getAttribute('data-dominio');
                return window.fetch(urlContador, { credentials: 'same-origin' })
                    .then(function (respuesta) {
                        return respuesta.ok ? respuesta.json() : null;
                    })
                    .then(function (datos) {
                        if (!datos) return { contador: 0, items: [] };
                        var visto = ultimoVisto(urlContador);
                        if (visto !== null && datos.contador > visto
                            && window.Notification
                            && window.Notification.permission === 'granted') {
                            var aviso = new window.Notification(
                                'Tienes nuevas novedades en ' + dominio,
                                {
                                    body: 'Hay novedades sin leer para revisar.',
                                    tag: urlContador,
                                });
                            aviso.onclick = function () {
                                window.focus();
                                window.location.href = urlPagina;
                            };
                        }
                        guardarVisto(urlContador, datos.contador);
                        var items = (datos.items || []).map(function (item) {
                            return {
                                mensaje: item.mensaje,
                                chip: item.chip,
                                fechaUtc: item.fechaUtc,
                                urlPagina: urlPagina,
                            };
                        });
                        return { contador: datos.contador, items: items };
                    })
                    .catch(function () {
                        /* Un sondeo fallido no molesta: el siguiente lo reintenta. */
                        return { contador: 0, items: [] };
                    });
            });

            Promise.all(pendientes).then(function (resultados) {
                var total = resultados.reduce(function (suma, r) {
                    return suma + r.contador;
                }, 0);
                if (badge) {
                    if (total > 0) {
                        badge.textContent = String(total);
                        badge.removeAttribute('hidden');
                    } else {
                        badge.setAttribute('hidden', '');
                    }
                }
                if (menu) {
                    var combinados = resultados
                        .reduce(function (acc, r) { return acc.concat(r.items); }, [])
                        .sort(function (a, b) {
                            return new Date(b.fechaUtc) - new Date(a.fechaUtc);
                        })
                        .slice(0, 5);
                    menu.innerHTML = '<ul class="novedades">' + (combinados.length
                        ? combinados.map(renderizarItem).join('')
                        : '<li class="vacio__texto">No hay notificaciones.</li>') + '</ul>';
                }
            });
        };

        sondearCampana();
        window.setInterval(sondearCampana, 30000);
    }

    /* Banner de permiso (spec 2026-10-05): se pide solo tras un clic
       explícito y nunca vuelve a insistir solo una vez que el gestor decide
       (conceder o negar), coherente con que el navegador tampoco deja volver
       a preguntar tras un rechazo. */
    var banner = document.querySelector('[data-banner-notificaciones]');
    if (banner && window.Notification) {
        var cuentaBanner = document.querySelector('.barra__cuenta');
        var correoBanner = cuentaBanner ? cuentaBanner.getAttribute('title') || '' : '';
        var claveBannerOculto = 'notificaciones-banner-oculto:' + correoBanner;
        var yaDecidido = window.Notification.permission !== 'default'
            || window.localStorage.getItem(claveBannerOculto) === 'true';
        if (!yaDecidido) {
            banner.removeAttribute('hidden');
            var botonActivar = banner.querySelector('[data-banner-activar]');
            if (botonActivar) {
                botonActivar.addEventListener('click', function () {
                    window.Notification.requestPermission().then(function () {
                        window.localStorage.setItem(claveBannerOculto, 'true');
                        banner.setAttribute('hidden', '');
                    });
                });
            }
        }
    }
```

- [ ] **Step 2: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/js/aplicacion.js
git commit -m "feat(gestor-caisy): sondeo global, campanita y permiso de notificaciones del navegador"
```

---

### Task 7: Verificación manual, puerta de calidad y push

- [ ] **Step 1: Levantar la aplicación localmente**

Run: `dotnet run --project Icarus/src/Apps/Trajano.GestorCaisy` (o el comando equivalente que use este repo para levantar `Trajano.GestorCaisy` en desarrollo; revisa `docs/ai/` si existe un script dedicado).

- [ ] **Step 2: Verificar el banner y el permiso**

En el navegador, con una cuenta de Gestor CAISY con alguna funcionalidad:
1. Primera carga: el banner «Activa los avisos del navegador...» debe aparecer.
2. Clic en «Activar notificaciones»: el navegador pide el permiso; concédelo. El banner desaparece.
3. Recarga la página: el banner no debe volver a aparecer.
4. Con las herramientas del navegador, resetea el permiso del sitio a "Preguntar" (en Chrome: candado en la barra de direcciones → Configuración del sitio → Notificaciones → Preguntar) y recarga: el banner debe volver a aparecer.
5. Repite rechazando el permiso: el banner debe desaparecer igual y no volver a aparecer en recargas siguientes.

- [ ] **Step 3: Verificar la campanita**

1. Con permiso concedido, provoca una novedad real (ej. que un cliente envíe un pedido) y confirma que, dentro de los 30 s siguientes, aparece un aviso nativo del navegador y el badge de la campanita se actualiza, aunque estés en `/Precios` en vez de `/Pedidos`.
2. Clic en el aviso nativo: debe enfocar la pestaña y navegar a `/Pedidos`.
3. Clic en la campanita: debe desplegar la lista con el mismo item.
4. Clic en el item de la campanita: debe navegar a `/Pedidos`.
5. Repite el punto 1 con el permiso denegado: el aviso nativo no debe aparecer, pero el badge y el desplegable de la campanita sí deben actualizarse igual — este es el criterio de aceptación 5 del spec.
6. Con una cuenta sin ninguna funcionalidad de CAISY, confirma que ni la campanita ni el banner aparecen en ninguna página.

- [ ] **Step 4: Ejecutar la puerta de calidad completa**

Run: `./verify.ps1`
Expected: PASS. Si falla, arreglar el contenido señalado — nunca relajar un gate ni usar `--no-verify`.

- [ ] **Step 5: Revisar el diff completo antes de empujar**

Run: `git log --oneline -7` y `git diff develop@{upstream}..HEAD --stat` (o el equivalente si la rama no tiene upstream todavía)
Expected: solo los 6 commits de este plan (Tasks 1 a 6), sin archivos inesperados.

- [ ] **Step 6: Pedir confirmación explícita al usuario antes de ejecutar `git push`**

Este repositorio trabaja sin pull requests: `develop` recibe commits y push directos, pero cada push se confirma con el usuario antes de ejecutarse. Una vez confirmado:

```bash
git push
```
