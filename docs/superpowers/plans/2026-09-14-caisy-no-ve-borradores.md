# CAISY no ve borradores — Plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para ejecutar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

**Objetivo:** que las bandejas y los detalles globales de CAISY dejen de
mostrar los borradores que el tenant todavía no envió.

**Arquitectura:** el filtro se aplica en un solo lugar por agregado (la
consulta CAISY del repositorio), antes de todo otro filtro y antes del
`CountAsync`. Para el detalle por id se crean queries propias de CAISY, porque
hoy las rutas del tenant y de CAISY comparten el mismo query y filtrar en el
handler común rompería la vista del cliente.

**Stack:** .NET 10, MediatR, EF Core 10, xUnit con Testcontainers.MsSql.

**Spec:** `docs/superpowers/specs/2026-09-14-caisy-no-ve-borradores-design.md`

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

## Advertencia: el otro «Borrador»

`Trajano.GestorCaisy` y sus pruebas usan la palabra `Borrador` para el
**borrador de una notificación de precios de alimento**, que es un documento
propio de CAISY. No tiene relación con este plan y **no se toca**. Si algo
fuera de los archivos listados en el mapa aparece en el diff, está mal.

## Advertencia: el cupo semanal

`OpcionesPedidosAlimento.MaximoPorSemana` vale **3 pedidos enviados por cliente
y semana ISO**, y las clases de prueba comparten la base de la colección. Los
tenants `cliente@icarus.test` y `c1@icarus.test` ya consumen su cupo en otras
pruebas del mismo archivo.

Por eso los tests de este plan usan **`c4@icarus.test`**
(`SemillaIdentidad.EmailClienteC4`): está sembrado con papel
`PapelCreditoDesarrollo.Vacio`, o sea que tiene granja activa (necesaria para
crear un pedido) pero ningún pedido ni despacho, y ninguna prueba de
integración lo usa hoy. Su cupo de 3 alcanza justo para la paginación.

## Mapa de archivos

| Archivo | Qué cambia |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPedidosAlimento.cs` | Excluir `Borrador` en `ListarPaginadoCaisyAsync` (línea 79) |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs` | Excluir `Borrador` en `ListarPaginadoCaisyAsync` (línea 61) |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs` | Nueva query y handler `ObtenerPedidoAlimentoCaisyQuery` |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs` | Nueva query y handler `ObtenerDespachoHuevoCaisyQuery` |
| `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs` | La ruta CAISY de detalle (líneas 149-151) usa la query nueva |
| `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs` | La ruta CAISY de detalle (líneas 117-118) usa la query nueva |
| `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs` | Reescribir `LaBandejaCaisyFiltraYPagina` y agregar pruebas |
| `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs` | Reescribir `LaBandejaCaisyListaFiltraPorEstadoYPagina` y agregar pruebas |

---

### Task 1: La bandeja CAISY de pedidos oculta los borradores

**Archivos:**
- Modificar: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPedidosAlimento.cs:79`
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consume: nada de tareas anteriores.
- Produce: nada que otras tareas consuman. Las cuatro tareas son
  independientes entre sí.

- [ ] **Paso 1: Reescribir la prueba existente `LaBandejaCaisyFiltraYPagina`**

Esta prueba **afirma hoy el comportamiento equivocado**: pide
`?estado=Borrador` y verifica que CAISY los ve. Usa borradores solo porque son
material barato para ejercitar la paginación.

**Esta reescritura es una prueba de caracterización: pasa en verde antes y
después del cambio de código.** No se puede ver en rojo, y eso es lo esperado.
Su valor es dejar de afirmar lo contrario de lo que queremos.

Reemplazar el método completo (líneas 270 a 308, desde `[Fact]` hasta la llave
de cierre del método) por:

```csharp
    [Fact]
    public async Task LaBandejaCaisyFiltraYPagina()
    {
        var cliente = _factory.CreateClient();
        // c4 tiene granja y cupo intacto: cliente@ y c1@ ya agotan su cupo
        // semanal en otras pruebas de este mismo archivo.
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);

        // Enviados, no borradores: CAISY solo ve lo que el tenant le mandó.
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await CrearBorradorAsync(cliente, tokenCliente);
            Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenCliente, id));
            ids.Add(id);
        }

        var pagina = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?estado=Solicitado&pagina=1&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var cuerpo = await pagina.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("total").GetInt32() >= 3);
        Assert.Equal(2, cuerpo.GetProperty("items").GetArrayLength());
        Assert.All(cuerpo.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("Solicitado", item.GetProperty("estado").GetString());
            Assert.NotEqual(Guid.Empty, Guid.Parse(item.GetProperty("clienteId").GetString()!));
        });

        var porPresentacion = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?presentacion=Bolsa&pagina=1&tamanoPagina=100",
            tokenCaisy));
        Assert.True((await porPresentacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32() >= 3);

        // La bandeja es global: CAISY abre el detalle de un pedido ajeno ya enviado.
        var detalle = await caisy.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{ids[0]}", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        var estadoInvalido = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?estado=Inexistente", tokenCaisy));
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);
    }
```

- [ ] **Paso 2: Ejecutar la prueba reescrita y verificar que pasa**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisyFiltraYPagina"
```

Esperado: PASS. Si falla, el problema está en la reescritura (cupo, granja o
precios), no en el código de producción, que todavía no se tocó.

- [ ] **Paso 3: Escribir la prueba nueva**

Agregar inmediatamente después del método reescrito:

```csharp
    // El borrador es trabajo en curso del tenant: CAISY no lo ve ni en la
    // bandeja sin filtro ni pidiéndolo por estado.
    [Fact]
    public async Task LaBandejaCaisyNoMuestraBorradoresDelTenant()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        var borrador = await CrearBorradorAsync(cliente, tokenCliente);

        var sinFiltro = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?pagina=1&tamanoPagina=100", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, sinFiltro.StatusCode);
        var cuerpoSinFiltro = await sinFiltro.Content.ReadFromJsonAsync<JsonElement>();
        var idsVisibles = cuerpoSinFiltro.GetProperty("items").EnumerateArray()
            .Select(p => Guid.Parse(p.GetProperty("id").GetString()!))
            .ToList();
        Assert.DoesNotContain(borrador, idsVisibles);
        Assert.DoesNotContain(cuerpoSinFiltro.GetProperty("items").EnumerateArray(),
            p => p.GetProperty("estado").GetString() == "Borrador");

        // Pedir el estado explícitamente no reabre la puerta.
        var porEstado = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?estado=Borrador&pagina=1&tamanoPagina=100",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, porEstado.StatusCode);
        var cuerpoPorEstado = await porEstado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, cuerpoPorEstado.GetProperty("total").GetInt32());
        Assert.Equal(0, cuerpoPorEstado.GetProperty("items").GetArrayLength());
    }
```

- [ ] **Paso 4: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisyNoMuestraBorradoresDelTenant"
```

Esperado: FAIL. El borrador aparece en la lista sin filtro y el total por
estado `Borrador` es mayor que cero.

- [ ] **Paso 5: Aplicar el filtro en el repositorio**

En `RepositorioPedidosAlimento.cs`, reemplazar la línea 79:

```csharp
        var consulta = db.PedidosAlimento.Include(p => p.Detalles).AsNoTracking();
```

por:

```csharp
        // CAISY ve el pedido desde que el tenant lo envía (spec 2026-09-14).
        // Va antes del filtro por estado a propósito: si estuviera dentro del
        // if, un ?estado=Borrador volvería a exponer la lista completa.
        var consulta = db.PedidosAlimento.Include(p => p.Detalles).AsNoTracking()
            .Where(p => p.Estado != EstadoPedidoAlimento.Borrador);
```

- [ ] **Paso 6: Ejecutar las dos pruebas y verificar que pasan**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisy"
```

Esperado: PASS en `LaBandejaCaisyFiltraYPagina` y en
`LaBandejaCaisyNoMuestraBorradoresDelTenant`.

- [ ] **Paso 7: Puerta de calidad y commit**

```
./verify.ps1
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPedidosAlimento.cs Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs
git commit -m "fix(caisy): la bandeja de pedidos deja de mostrar borradores del tenant"
```

---

### Task 2: La bandeja CAISY de despachos oculta los borradores

**Archivos:**
- Modificar: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs:61`
- Test: `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs`

**Interfaces:**
- Consume: nada. Independiente de la Task 1.
- Produce: nada.

- [ ] **Paso 1: Reescribir la prueba existente `LaBandejaCaisyListaFiltraPorEstadoYPagina`**

Igual que en la Task 1, esta prueba afirma hoy lo contrario de lo que
queremos: pagina sobre `?estado=Borrador`. **Es una prueba de caracterización:
pasa en verde antes y después, no se puede ver en rojo.**

El cambio de fondo es que ahora se despachan los tres, no solo el primero.

Reemplazar el método completo (líneas 289 a 343, desde `[Fact]` hasta la llave
de cierre del método) por:

```csharp
    [Fact]
    public async Task LaBandejaCaisyListaFiltraPorEstadoYPagina()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        await ImportarYPublicarHuevoAsync(cliente, tokenCaisy);

        // Los tres se despachan: CAISY solo ve lo que salió del borrador.
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await CrearBorradorAsync(cliente, tokenCliente);
            await DespacharAsync(cliente, tokenCliente, id);
            ids.Add(id);
        }

        var primera = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Despachado&pagina=1&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var cuerpoPrimera = await primera.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpoPrimera.GetProperty("total").GetInt32() >= 3);
        var itemsPrimera = cuerpoPrimera.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, itemsPrimera.Count);
        Assert.All(itemsPrimera, d => Assert.Equal("Despachado", d.GetProperty("estado").GetString()));

        var segunda = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Despachado&pagina=2&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var cuerpoSegunda = await segunda.Content.ReadFromJsonAsync<JsonElement>();
        var vistos = itemsPrimera.Concat(cuerpoSegunda.GetProperty("items").EnumerateArray())
            .Select(d => Guid.Parse(d.GetProperty("id").GetString()!))
            .ToList();
        Assert.Contains(ids[1], vistos);
        Assert.Contains(ids[2], vistos);

        // Estado inexistente: 400 con mensaje genérico.
        var estadoInvalido = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Inexistente", tokenCaisy));
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);

        // Detalle global: CAISY ve el despacho congelado del tenant.
        var detalle = await ObtenerDetalleCaisyAsync(cliente, tokenCaisy, ids[0]);
        Assert.Equal("Despachado", detalle.GetProperty("estado").GetString());
        Assert.True(detalle.GetProperty("totalBs").GetDecimal() > 0);
    }
```

- [ ] **Paso 2: Ejecutar la prueba reescrita y verificar que pasa**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisyListaFiltraPorEstadoYPagina"
```

Esperado: PASS.

- [ ] **Paso 3: Escribir la prueba nueva**

Agregar inmediatamente después del método reescrito:

```csharp
    // El borrador de despacho es trabajo en curso del tenant: CAISY no lo ve
    // ni en la bandeja sin filtro ni pidiéndolo por estado.
    [Fact]
    public async Task LaBandejaCaisyNoMuestraBorradoresDeDespacho()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        var borrador = await CrearBorradorAsync(cliente, tokenCliente);

        var sinFiltro = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?pagina=1&tamanoPagina=100", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, sinFiltro.StatusCode);
        var cuerpoSinFiltro = await sinFiltro.Content.ReadFromJsonAsync<JsonElement>();
        var idsVisibles = cuerpoSinFiltro.GetProperty("items").EnumerateArray()
            .Select(d => Guid.Parse(d.GetProperty("id").GetString()!))
            .ToList();
        Assert.DoesNotContain(borrador, idsVisibles);
        Assert.DoesNotContain(cuerpoSinFiltro.GetProperty("items").EnumerateArray(),
            d => d.GetProperty("estado").GetString() == "Borrador");

        var porEstado = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Borrador&pagina=1&tamanoPagina=100",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, porEstado.StatusCode);
        var cuerpoPorEstado = await porEstado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, cuerpoPorEstado.GetProperty("total").GetInt32());
        Assert.Equal(0, cuerpoPorEstado.GetProperty("items").GetArrayLength());
    }
```

- [ ] **Paso 4: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisyNoMuestraBorradoresDeDespacho"
```

Esperado: FAIL. El borrador aparece y el total por estado `Borrador` es mayor
que cero.

- [ ] **Paso 5: Aplicar el filtro en el repositorio**

En `RepositorioDespachosHuevo.cs`, reemplazar la línea 61:

```csharp
        var consulta = db.DespachosHuevo.Include(d => d.Detalles).AsNoTracking();
```

por:

```csharp
        // CAISY ve el despacho desde que el tenant lo envía (spec 2026-09-14).
        // Va antes del filtro por estado a propósito: si estuviera dentro del
        // if, un ?estado=Borrador volvería a exponer la lista completa.
        var consulta = db.DespachosHuevo.Include(d => d.Detalles).AsNoTracking()
            .Where(d => d.Estado != EstadoDespachoHuevo.Borrador);
```

- [ ] **Paso 6: Ejecutar las dos pruebas y verificar que pasan**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~LaBandejaCaisy"
```

Esperado: PASS.

- [ ] **Paso 7: Puerta de calidad y commit**

```
./verify.ps1
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs
git commit -m "fix(caisy): la bandeja de despachos deja de mostrar borradores del tenant"
```

---

### Task 3: El detalle CAISY de un pedido borrador responde 404

**Archivos:**
- Modificar: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`
- Modificar: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs:149-151`
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consume: nada de tareas anteriores.
- Produce: `ObtenerPedidoAlimentoCaisyQuery(Guid PedidoId) : IRequest<PedidoAlimentoDetalle>`.

**Por qué una query nueva y no un `if` en el handler existente:** hoy la ruta
del tenant (`PedidosAlimentoEndpoints.cs:70`) y la de CAISY (línea 151)
despachan **la misma** `ObtenerPedidoAlimentoQuery`. Filtrar dentro de
`ObtenerPedidoAlimentoHandler` le sacaría al Cliente la vista de sus propios
borradores, que es justamente lo que tiene que seguir funcionando.

- [ ] **Paso 1: Escribir la prueba que falla**

Agregar al final de la clase, antes de la llave de cierre:

```csharp
    // Si el borrador no se ve en la bandeja, tampoco por URL directa. Responde
    // 404 y no 403: un 403 confirmaría que el registro existe.
    [Fact]
    public async Task ElDetalleCaisyDeUnBorradorResponde404YElTenantLoSigueViendo()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        var borrador = await CrearBorradorAsync(cliente, tokenCliente);

        var vistaCaisy = await caisy.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{borrador}", tokenCaisy));
        Assert.Equal(HttpStatusCode.NotFound, vistaCaisy.StatusCode);

        // El dueño sigue viendo su propio borrador: el filtro es solo de CAISY.
        var vistaTenant = await ObtenerDetalleAsync(cliente, tokenCliente, borrador);
        Assert.Equal("Borrador", vistaTenant.GetProperty("estado").GetString());
    }
```

- [ ] **Paso 2: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~ElDetalleCaisyDeUnBorradorResponde404"
```

Esperado: FAIL con 200 OK donde se esperaba 404.

- [ ] **Paso 3: Agregar la query de CAISY**

En `ComandosPedidosAlimento.cs`, justo después de la declaración de
`ObtenerPedidoAlimentoQuery` (líneas 109-110), agregar:

```csharp
// Detalle para la bandeja global de CAISY (spec 2026-09-14). Existe aparte de
// ObtenerPedidoAlimentoQuery porque el tenant sí tiene que ver sus borradores
// y las dos rutas compartían el mismo query.
public sealed record ObtenerPedidoAlimentoCaisyQuery(Guid PedidoId)
    : IRequest<PedidoAlimentoDetalle>;
```

En el mismo archivo, justo después de `ObtenerPedidoAlimentoHandler` (que
termina en la línea 756), agregar:

```csharp
public sealed class ObtenerPedidoAlimentoCaisyHandler(IRepositorioPedidosAlimento repositorio)
    : IRequestHandler<ObtenerPedidoAlimentoCaisyQuery, PedidoAlimentoDetalle>
{
    public async Task<PedidoAlimentoDetalle> Handle(
        ObtenerPedidoAlimentoCaisyQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerConHistorialAsync(request.PedidoId, cancellationToken);
        // Un borrador se trata como inexistente: el 404 no revela que está ahí.
        if (pedido is null || pedido.Estado == EstadoPedidoAlimento.Borrador)
            throw new NotFoundException("Pedido de alimento", request.PedidoId);
        return MapeadorPedidos.MapearDetalle(pedido);
    }
}
```

- [ ] **Paso 4: Apuntar la ruta de CAISY a la query nueva**

En `PedidosAlimentoEndpoints.cs`, reemplazar las líneas 149 a 151:

```csharp
        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPedidoAlimentoQuery(id), cancellationToken)));
```

por:

```csharp
        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPedidoAlimentoCaisyQuery(id), cancellationToken)));
```

**No tocar la línea 70**, que es la ruta del tenant y debe seguir usando
`ObtenerPedidoAlimentoQuery`.

- [ ] **Paso 5: Ejecutar la clase completa y verificar que pasa**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidosAlimentoEndpointsTests"
```

Esperado: PASS en toda la clase. Si `LaBandejaCaisyFiltraYPagina` falla acá,
es porque su detalle usa `ids[0]`, que en la versión reescrita de la Task 1 ya
está enviado; si sigue en borrador, la Task 1 quedó a medias.

- [ ] **Paso 6: Puerta de calidad y commit**

```
./verify.ps1
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs
git commit -m "fix(caisy): el detalle de un pedido borrador responde 404"
```

---

### Task 4: El detalle CAISY de un despacho borrador responde 404

**Archivos:**
- Modificar: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs`
- Modificar: `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs:117-118`
- Test: `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs`

**Interfaces:**
- Consume: nada de tareas anteriores.
- Produce: `ObtenerDespachoHuevoCaisyQuery(Guid DespachoId) : IRequest<DespachoHuevoDetalle>`.

Mismo motivo que en la Task 3: la ruta del tenant y la de CAISY comparten hoy
`ObtenerDespachoHuevoQuery`, y el tenant debe seguir viendo sus borradores.

- [ ] **Paso 1: Escribir la prueba que falla**

Agregar al final de la clase, antes de la llave de cierre:

```csharp
    // Si el borrador no se ve en la bandeja, tampoco por URL directa.
    [Fact]
    public async Task ElDetalleCaisyDeUnDespachoBorradorResponde404()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC4);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        var borrador = await CrearBorradorAsync(cliente, tokenCliente);

        var vistaCaisy = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/despachos-huevo-caisy/{borrador}", tokenCaisy));
        Assert.Equal(HttpStatusCode.NotFound, vistaCaisy.StatusCode);

        // El dueño sigue viendo su propio borrador.
        var vistaTenant = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/despachos-huevo/{borrador}", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, vistaTenant.StatusCode);
        var cuerpoTenant = await vistaTenant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Borrador", cuerpoTenant.GetProperty("estado").GetString());
    }
```

- [ ] **Paso 2: Ejecutar y verificar que falla**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~ElDetalleCaisyDeUnDespachoBorradorResponde404"
```

Esperado: FAIL con 200 OK donde se esperaba 404.

- [ ] **Paso 3: Agregar la query de CAISY**

En `ComandosDespachosHuevo.cs`, justo después del record
`PaginaDespachosHuevo` (línea 264), agregar:

```csharp
// Detalle para la bandeja global de CAISY (spec 2026-09-14). Existe aparte de
// ObtenerDespachoHuevoQuery porque el tenant sí tiene que ver sus borradores
// y las dos rutas compartían el mismo query.
public sealed record ObtenerDespachoHuevoCaisyQuery(Guid DespachoId)
    : IRequest<DespachoHuevoDetalle>;
```

Y justo después de `ObtenerDespachoHuevoHandler` (que termina en la línea
300), agregar:

```csharp
public sealed class ObtenerDespachoHuevoCaisyHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ObtenerDespachoHuevoCaisyQuery, DespachoHuevoDetalle>
{
    public async Task<DespachoHuevoDetalle> Handle(
        ObtenerDespachoHuevoCaisyQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(
            request.DespachoId, cancellationToken);
        // Un borrador se trata como inexistente: el 404 no revela que está ahí.
        if (despacho is null || despacho.Estado == EstadoDespachoHuevo.Borrador)
            throw new NotFoundException("Despacho de huevo", request.DespachoId);
        return new DespachoHuevoDetalle(
            despacho.Id, despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs,
            despacho.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetalleDespachoHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.CantidadAmarras, d.UnidadesSueltas,
                    d.CantidadHuevos, d.PrecioUnitarioCongelado, d.Subtotal))
                .ToList());
    }
}
```

- [ ] **Paso 4: Apuntar la ruta de CAISY a la query nueva**

En `DespachosHuevoEndpoints.cs`, reemplazar las líneas 117 y 118:

```csharp
        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoQuery(id), cancellationToken)));
```

por:

```csharp
        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoCaisyQuery(id), cancellationToken)));
```

No tocar la ruta equivalente del grupo `tenant`.

- [ ] **Paso 5: Ejecutar la clase completa y verificar que pasa**

Ejecutar:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~DespachosHuevoCaisyEndpointsTests"
```

Esperado: PASS en toda la clase.

- [ ] **Paso 6: Puerta de calidad, commit y push**

```
./verify.ps1
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs
git commit -m "fix(caisy): el detalle de un despacho borrador responde 404"
git push
```

---

## Verificación manual

Con `./iniciar-pc1.ps1` (sin `-RecrearDatos`: este plan no agrega columnas ni
migraciones). Contraseña de la semilla en pc1: `Admin123!`.

1. Entrar como `cliente@icarus.test` y crear un pedido de alimento sin
   enviarlo. Entrar como gestor con `GestorPedidoAlimento` y confirmar que no
   aparece en la bandeja, ni con el filtro de estado en «Borrador».
2. Copiar el id del borrador de la URL del cliente y pedirlo directo en
   `/pedidos-alimento-caisy/{id}` desde la cuenta del gestor: debe dar 404.
3. Repetir los dos pasos con un despacho de huevo y un gestor con
   `GestorRecepcionHuevos`.
4. Enviar el pedido y confirmar que aparece de inmediato en la bandeja del
   gestor.
5. Como gestor, devolver ese pedido. **Comportamiento esperado y deliberado:**
   el pedido desaparece de la bandeja de CAISY, porque la devolución lo regresa
   a `Borrador`. Reaparece cuando el cliente lo reenvía.
6. Confirmar que el catálogo de precios de alimento del MVC sigue funcionando:
   sus borradores son otra cosa y no debieron cambiar.
