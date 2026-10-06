# Entitlement con múltiples módulos y limpieza de vistas de precios — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corregir el bug de autorización que deniega 403 a trabajadores de
Clientes con más de un módulo habilitado, y limpiar dos vistas de precios en
Trajano.GestorCaisy (quitar la advertencia "Precio actual esperado" y
destacar la publicación vigente en el historial).

**Architecture:** Tres correcciones independientes mínimas sobre código
existente: (1) una función pura en `Icarus.Clientes.Domain` pasa de un
`switch` de valor exacto a una iteración de flags; (2) eliminación simétrica
de un campo calculado (`PrecioAnteriorEsperado`) a través de
Application → contrato del MVC → vistas → CSS, para alimento y huevo; (3) un
wrapper de vista nuevo (mismo patrón que `VistaDetalles.Crear`) que calcula
localmente cuál fila del historial ya recibido es la vigente, sin tocar la
API.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, MediatR, xUnit +
NSubstitute, Testcontainers.MsSql (pruebas de integración), ASP.NET Core MVC
Razor (Trajano.GestorCaisy), CSS plano.

## Global Constraints

- Documentos, identificadores y texto de UI en español correcto, con acentos,
  UTF-8 sin BOM.
- Anti-PII: no se toca ningún dato biométrico ni nominal en este plan.
- `./verify.ps1` (o `./verify.sh`) debe quedar verde antes de cada commit;
  prohibido `--no-verify`.
- TDD estricto: cada test nuevo se debe ver fallar antes de implementar.
- Commits frecuentes y pequeños, uno por tarea.
- No ampliar el alcance: no se tocan el PWA, la API de Host más allá de lo
  listado, ni ninguna otra vista de GestorCaisy.

---

## Task 1: Corregir `FuncionalidadesModulos.FuncionalidadesDelModulo` para módulos combinados

**Files:**
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/ConsultaPermisosActuales.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs`

**Interfaces:**
- Consumes: `Modulos` (flags: `Ninguno=0`, `GestionAvicola=1`,
  `ControlAcceso=2`, en `Icarus.Clientes.Domain.Modulos`), `Funcionalidades`
  (flags, en `Icarus.Clientes.Domain.Funcionalidades`).
- Produces: `FuncionalidadesModulos.FuncionalidadesDelModulo(Modulos modulos)`
  sigue teniendo la misma firma pública (el parámetro ahora puede ser una
  combinación, no solo un valor individual) — ningún llamador externo cambia
  de firma.

- [ ] **Step 1: Escribir el test unitario que falla (combinación de módulos)**

Abre `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs` y agrega
este test al final de la clase, antes del `}` de cierre:

```csharp
    [Fact]
    public void FuncionalidadesDelModuloConCombinacionIncluyeLasDeGestionAvicola()
    {
        var combinado = FuncionalidadesModulos.FuncionalidadesDelModulo(
            Modulos.GestionAvicola | Modulos.ControlAcceso);

        Assert.Equal(
            FuncionalidadesModulos.FuncionalidadesDelModulo(Modulos.GestionAvicola),
            combinado);
        Assert.True(combinado.HasFlag(Funcionalidades.PedidoAlimento));
    }
```

- [ ] **Step 2: Ejecutar el test y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~FuncionalidadesTests.FuncionalidadesDelModuloConCombinacionIncluyeLasDeGestionAvicola`
Expected: FAIL — el `switch` actual devuelve `Funcionalidades.Ninguno` para
`GestionAvicola | ControlAcceso` porque no matchea ningún caso exacto.

- [ ] **Step 3: Corregir `FuncionalidadesModulos.FuncionalidadesDelModulo`**

Reemplaza el método completo en
`Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`:

```csharp
    public static Funcionalidades FuncionalidadesDelModulo(Modulos modulos)
    {
        var acumulado = Funcionalidades.Ninguno;
        foreach (var modulo in Enum.GetValues<Modulos>())
            if (modulo != Modulos.Ninguno && modulos.HasFlag(modulo))
                acumulado |= FuncionalidadesDeUnModulo(modulo);
        return acumulado;
    }

    private static Funcionalidades FuncionalidadesDeUnModulo(Modulos modulo) => modulo switch
    {
        Modulos.GestionAvicola => Funcionalidades.Granjas | Funcionalidades.Galpones
            | Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad
            | Funcionalidades.Vacunacion | Funcionalidades.Alimentacion
            | Funcionalidades.Despachos | Funcionalidades.Precios
            | Funcionalidades.PedidoAlimento | Funcionalidades.DespachoHuevo,
        _ => Funcionalidades.Ninguno,
    };
```

(Reemplaza el `public static Funcionalidades FuncionalidadesDelModulo(Modulos modulo) => modulo switch { ... };`
original por este bloque; el parámetro se renombra de `modulo` a `modulos`
porque ahora acepta una combinación.)

- [ ] **Step 4: Ejecutar el test unitario y verificar que pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~FuncionalidadesTests`
Expected: PASS — incluido el test existente
`ControlAccesoNoTieneFuncionalidades` (sigue pasando: un único flag
`ControlAcceso` da `Ninguno`).

- [ ] **Step 5: Simplificar `ConsultaPermisosActuales` para usar la implementación corregida**

`ConsultaPermisosActuales.cs` tiene hoy su propia copia de esta iteración en
un método privado `FuncionalidadesDe`. Edita el archivo para eliminar la
duplicación:

Reemplaza:

```csharp
            var efectivas = contexto.Funcionalidades
                & FuncionalidadesTrabajador.Asignables
                & FuncionalidadesDe(contexto.ModulosHabilitados);
```

por:

```csharp
            var efectivas = contexto.Funcionalidades
                & FuncionalidadesTrabajador.Asignables
                & FuncionalidadesModulos.FuncionalidadesDelModulo(contexto.ModulosHabilitados);
```

Reemplaza:

```csharp
        var habilitados = modulos ?? Modulos.Ninguno;
        return new PermisosActuales(NombresModulos(habilitados), NombresFuncionalidades(FuncionalidadesDe(habilitados)));
```

por:

```csharp
        var habilitados = modulos ?? Modulos.Ninguno;
        return new PermisosActuales(
            NombresModulos(habilitados),
            NombresFuncionalidades(FuncionalidadesModulos.FuncionalidadesDelModulo(habilitados)));
```

Y elimina por completo el método privado que queda sin uso:

```csharp
    private static Funcionalidades FuncionalidadesDe(Modulos modulos)
    {
        var acumulado = Funcionalidades.Ninguno;
        foreach (var modulo in Enum.GetValues<Modulos>())
            if (modulo != Modulos.Ninguno && modulos.HasFlag(modulo))
                acumulado |= FuncionalidadesModulos.FuncionalidadesDelModulo(modulo);
        return acumulado;
    }
```

Agrega `using Icarus.Clientes.Domain;` si el archivo no lo tiene ya (debería
tenerlo, porque usa `Funcionalidades`/`Modulos` directamente).

- [ ] **Step 6: Ejecutar las pruebas unitarias de Clientes y verificar que todo pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~Clientes`
Expected: PASS — en particular `ObtenerPermisosActualesHandlerTests` sigue
pasando sin cambios, porque el resultado observable de
`ConsultaPermisosActuales` no cambió, solo se quitó la duplicación interna.

- [ ] **Step 7: Escribir el test de integración de regresión (end-to-end, el que prueba el bug real)**

Abre `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs` y agrega este
test al final de la clase, antes del `}` de cierre:

```csharp
    [Fact]
    public async Task TrabajadorDeClienteConModulosCombinadosMantieneSusFuncionalidades()
    {
        // Bug 2026-10-05: FuncionalidadesModulos.FuncionalidadesDelModulo
        // (switch de valor exacto) devolvía Ninguno para cualquier
        // combinación de módulos, dejando sin acceso a trabajadores de
        // Clientes con más de un módulo habilitado.
        var (clienteId, tokenCliente) = await CrearClienteConCuenta(
            ["GestionAvicola", "ControlAcceso"]);
        var (_, tokenTrabajador, _) = await CrearTrabajadorConCuenta(
            clienteId, ["produccionhuevos"], tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/clientes/sondeo/funcionalidad/produccionhuevos", tokenTrabajador));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
```

- [ ] **Step 8: Ejecutar el test de integración y verificar que pasa (requiere Docker corriendo)**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~EntitlementTests.TrabajadorDeClienteConModulosCombinadosMantieneSusFuncionalidades`
Expected: PASS. Si aún no habías aplicado el Step 3, este test debía fallar
con 403 en vez de 200 — si tienes dudas, puedes revertir temporalmente el
Step 3 y confirmar el rojo antes de reaplicarlo.

- [ ] **Step 9: Commit**

```bash
git add Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs \
  Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/ConsultaPermisosActuales.cs \
  Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs \
  Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs
git commit -m "fix(clientes): corrige entitlement cuando el cliente tiene varios modulos"
```

---

## Task 2: Quitar `PrecioAnteriorEsperado` del backend de precios de alimento

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`

**Interfaces:**
- Consumes: ninguno nuevo.
- Produces: `DetallePrecioResumen` sin el campo `PrecioAnteriorEsperado`;
  `MapeadorPrecios.Mapear(NotificacionPreciosAlimentos notificacion)` (sin
  segundo parámetro `anterior`). Las Tasks 4 y posteriores que toquen el MVC
  dependen de que `DetallePrecioApi` (del lado del MVC) ya no tenga ese campo
  — eso es la Task 4.

- [ ] **Step 1: Localizar y adaptar los tests existentes que usan `PrecioAnteriorEsperado`**

Abre `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`
y busca todas las referencias a `PrecioAnteriorEsperado` (aserciones sobre
ese campo en `DetallePrecioResumen`, o construcciones que pasan un
`anterior` a `MapeadorPrecios.Mapear`, si el mapeador se prueba ahí
directamente). Elimina esas aserciones y, si algún test construye
explícitamente una segunda `NotificacionPreciosAlimentos` solo para servir de
"anterior", elimina esa configuración también. No borres el resto del test:
solo la parte relacionada con este campo. Si un test entero no tiene más
propósito que probar `PrecioAnteriorEsperado`, elimínalo por completo.

- [ ] **Step 2: Ejecutar los tests de este archivo y verificar que fallan (compilación)**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: FAIL en compilación — el archivo de test ya no referencia
`PrecioAnteriorEsperado`, pero `DetallePrecioResumen` todavía lo exige como
miembro del record en algunos `new(...)` posicionales si los hay. Si el
archivo compila igual en este punto (porque no quedaban construcciones
posicionales de `DetallePrecioResumen` en los tests), continúa directo al
Step 3 — no hay paso en rojo artificial que inventar.

- [ ] **Step 3: Quitar el campo y el cálculo en el backend de alimento**

En `ComandosPreciosAlimentos.cs`:

Reemplaza:

```csharp
public sealed record DetallePrecioResumen(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias, string Codigo,
    decimal? PrecioAnteriorEsperado = null);
```

por:

```csharp
public sealed record DetallePrecioResumen(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias, string Codigo);
```

Reemplaza el cuerpo de `ObtenerNotificacionPreciosHandler.Handle`:

```csharp
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        var anterior = await repositorio.ObtenerVigenteAsync(notificacion.FechaDocumento, cancellationToken);
        return MapeadorPrecios.Mapear(notificacion, anterior);
```

por:

```csharp
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        return MapeadorPrecios.Mapear(notificacion);
```

Reemplaza el cuerpo de `ObtenerPrecioVigenteHandler.Handle`:

```csharp
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        if (vigente is null)
            return null;
        var anterior = await repositorio.ObtenerVigenteAsync(vigente.FechaDocumento, cancellationToken);
        return MapeadorPrecios.Mapear(vigente, anterior);
```

por:

```csharp
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        return vigente is null ? null : MapeadorPrecios.Mapear(vigente);
```

Reemplaza `MapeadorPrecios` completo:

```csharp
internal static class MapeadorPrecios
{
    public static NotificacionPreciosDetalle Mapear(
        NotificacionPreciosAlimentos notificacion, NotificacionPreciosAlimentos? anterior = null)
    {
        var preciosAnteriores = new Dictionary<(TipoAlimento, PresentacionAlimento), decimal>();
        if (anterior is not null)
            foreach (var detalle in anterior.Detalles)
                preciosAnteriores[(detalle.TipoAlimento, detalle.Presentacion)] = detalle.PrecioFinalPor40Kg;
        return new(notificacion.Id, notificacion.FechaDocumento, notificacion.VigenteDesde,
            notificacion.Estado.ToString(), notificacion.AporteCaisy, notificacion.Fondo,
            notificacion.Servicios, notificacion.DocumentoOriginalId,
            notificacion.Detalles
                .OrderBy(d => d.TipoAlimento).ThenBy(d => d.Presentacion)
                .Select(d => new DetallePrecioResumen(
                    d.Id, d.TipoAlimento.ToString(), d.Presentacion.ToString(),
                    d.PrecioFinalPor40Kg, d.PrecioActualDocumento, d.EdadDesdeDias, d.EdadHastaDias,
                    CatalogoAlimentosCaisy.CodigoDe(d.TipoAlimento, d.Presentacion),
                    preciosAnteriores.TryGetValue((d.TipoAlimento, d.Presentacion), out var precioAnterior)
                        ? precioAnterior
                        : null))
                .ToList());
    }
}
```

por:

```csharp
internal static class MapeadorPrecios
{
    public static NotificacionPreciosDetalle Mapear(NotificacionPreciosAlimentos notificacion) =>
        new(notificacion.Id, notificacion.FechaDocumento, notificacion.VigenteDesde,
            notificacion.Estado.ToString(), notificacion.AporteCaisy, notificacion.Fondo,
            notificacion.Servicios, notificacion.DocumentoOriginalId,
            notificacion.Detalles
                .OrderBy(d => d.TipoAlimento).ThenBy(d => d.Presentacion)
                .Select(d => new DetallePrecioResumen(
                    d.Id, d.TipoAlimento.ToString(), d.Presentacion.ToString(),
                    d.PrecioFinalPor40Kg, d.PrecioActualDocumento, d.EdadDesdeDias, d.EdadHastaDias,
                    CatalogoAlimentosCaisy.CodigoDe(d.TipoAlimento, d.Presentacion)))
                .ToList());
}
```

Busca el comentario justo encima de `DetallePrecioResumen` que explica
`PrecioAnteriorEsperado` (empieza con "// PrecioAnteriorEsperado...") y
bórralo junto con el campo: ya no aplica.

- [ ] **Step 4: Ejecutar los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs
git commit -m "refactor(precios-alimento): quita el calculo de precio anterior esperado"
```

---

## Task 3: Quitar `PrecioAnteriorEsperado` del backend de precios de huevo

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: ninguno nuevo.
- Produces: `DetallePrecioHuevoResumen` sin `PrecioAnteriorEsperado`;
  `MapeadorPreciosHuevo.Mapear(PublicacionPrecioHuevo publicacion)` (sin
  segundo parámetro).

- [ ] **Step 1: Adaptar los tests existentes que usan `PrecioAnteriorEsperado`**

Igual que en la Task 2 pero en
`Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosHuevoHandlerTests.cs`:
quita las aserciones y configuraciones relacionadas con
`PrecioAnteriorEsperado`, sin tocar el resto de cada test.

- [ ] **Step 2: Ejecutar los tests y verificar que fallan si corresponde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosHuevoHandlerTests`
Expected: igual que en la Task 2 — si no queda ninguna construcción
posicional pendiente, pasa directo al Step 3.

- [ ] **Step 3: Quitar el campo y el cálculo en el backend de huevo**

En `ComandosPreciosHuevo.cs`:

Reemplaza:

```csharp
public sealed record DetallePrecioHuevoResumen(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario, decimal? PrecioAnteriorEsperado = null);
```

por:

```csharp
public sealed record DetallePrecioHuevoResumen(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario);
```

Borra también el comentario inmediatamente anterior que empieza con
"PrecioAnteriorEsperado es informativo...".

Reemplaza el cuerpo de `ObtenerPublicacionPrecioHuevoHandler.Handle`:

```csharp
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        var anterior = await repositorio.ObtenerVigenteAsync(
            publicacion.FechaNotificacion, cancellationToken);
        return MapeadorPreciosHuevo.Mapear(publicacion, anterior);
```

por:

```csharp
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        return MapeadorPreciosHuevo.Mapear(publicacion);
```

Reemplaza el cuerpo de `ObtenerPrecioHuevoVigenteHandler.Handle`:

```csharp
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        if (vigente is null)
            return null;
        var anterior = await repositorio.ObtenerVigenteAsync(
            vigente.FechaNotificacion, cancellationToken);
        return MapeadorPreciosHuevo.Mapear(vigente, anterior);
```

por:

```csharp
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        return vigente is null ? null : MapeadorPreciosHuevo.Mapear(vigente);
```

Reemplaza `MapeadorPreciosHuevo` completo:

```csharp
internal static class MapeadorPreciosHuevo
{
    public static PublicacionPrecioHuevoDetalle Mapear(
        PublicacionPrecioHuevo publicacion, PublicacionPrecioHuevo? anterior = null)
    {
        var preciosAnteriores = new Dictionary<TamanoHuevo, decimal>();
        if (anterior is not null)
            foreach (var detalle in anterior.Detalles)
                preciosAnteriores[detalle.Tamano] = detalle.PrecioAlProductor;
        return new(publicacion.Id, publicacion.FechaNotificacion, publicacion.FechaVigencia,
            publicacion.Estado.ToString(), publicacion.Servicio, publicacion.DocumentoOriginalId,
            publicacion.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetallePrecioHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.PrecioAlProductor, d.PrecioActualDocumento,
                    d.PrecioAlProductor + publicacion.Servicio,
                    preciosAnteriores.TryGetValue(d.Tamano, out var precioAnterior)
                        ? precioAnterior
                        : null))
                .ToList());
    }
}
```

por:

```csharp
internal static class MapeadorPreciosHuevo
{
    public static PublicacionPrecioHuevoDetalle Mapear(PublicacionPrecioHuevo publicacion) =>
        new(publicacion.Id, publicacion.FechaNotificacion, publicacion.FechaVigencia,
            publicacion.Estado.ToString(), publicacion.Servicio, publicacion.DocumentoOriginalId,
            publicacion.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetallePrecioHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.PrecioAlProductor, d.PrecioActualDocumento,
                    d.PrecioAlProductor + publicacion.Servicio))
                .ToList());
}
```

- [ ] **Step 4: Ejecutar los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosHuevoHandlerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosHuevoHandlerTests.cs
git commit -m "refactor(precios-huevo): quita el calculo de precio anterior esperado"
```

---

## Task 4: Quitar `PrecioAnteriorEsperado` del contrato y de las vistas de GestorCaisy

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Detalles.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css`
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`

**Interfaces:**
- Consumes: Tasks 2 y 3 ya quitaron el campo del lado de la API (el MVC
  consume la API por HTTP; este cambio del contrato del cliente debe
  reflejar esa forma nueva de los DTO, aunque ambos lados se compilan y
  prueban por separado).
- Produces: `DetallePrecioApi` y `DetallePrecioHuevoApi` sin
  `PrecioAnteriorEsperado`.

- [ ] **Step 1: Quitar el campo del contrato del MVC**

En `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs`,
reemplaza:

```csharp
// PrecioAnteriorEsperado (spec 2026-09-15, alineado con precios de huevo) es
// el PrecioFinalPor40Kg vigente a la fecha del documento para el mismo tipo y
// presentación; solo alimenta la advertencia visual y nunca bloquea publicar.
public sealed record DetallePrecioApi(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias,
    decimal? PrecioAnteriorEsperado = null);
```

por:

```csharp
public sealed record DetallePrecioApi(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias);
```

Reemplaza:

```csharp
// PrecioAnteriorEsperado (spec 2026-09-15) es el PrecioAlProductor vigente a
// la fecha de notificación para el mismo tamaño; solo alimenta la advertencia
// visual y nunca bloquea publicar. Anulable para no romper respuestas de una
// API que aún no lo envía.
public sealed record DetallePrecioHuevoApi(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario, decimal? PrecioAnteriorEsperado = null);
```

por:

```csharp
public sealed record DetallePrecioHuevoApi(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario);
```

- [ ] **Step 2: Adaptar las fábricas de datos de prueba**

En `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`, dentro
de `CrearDetalle`, quita el último argumento posicional de cada
`DetallePrecioApi`:

Reemplaza:

```csharp
                new DetallePrecioApi(
                    Guid.NewGuid(), "Preiniciador", "Bolsa", 118.50m, 115.00m, 1, 21, 117.00m),
                new DetallePrecioApi(
                    Guid.NewGuid(), "PosturaDos", "Granel", 112.75m, 110.25m, null, null, 110.25m),
```

por:

```csharp
                new DetallePrecioApi(
                    Guid.NewGuid(), "Preiniciador", "Bolsa", 118.50m, 115.00m, 1, 21),
                new DetallePrecioApi(
                    Guid.NewGuid(), "PosturaDos", "Granel", 112.75m, 110.25m, null, null),
```

Y dentro de `CrearDetalleHuevo`, reemplaza:

```csharp
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Primera", 0.045m, 0.044m, 0.545m, 0.0445m),
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Extra", 0.050m, 0.049m, 0.550m, 0.049m),
```

por:

```csharp
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Primera", 0.045m, 0.044m, 0.545m),
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Extra", 0.050m, 0.049m, 0.550m),
```

Los comentarios encima de cada fábrica ("La fila Preiniciador difiere del
precio anterior esperado...") ya no aplican: bórralos o reescríbelos como un
comentario neutro si quieres conservar la intención de "dos filas distintas
para cubrir casos variados" (opcional, no bloqueante).

- [ ] **Step 3: Quitar la columna y la advertencia de `Precios/Detalles.cshtml`**

Reemplaza el bloque `@{ ... }` del encabezado (líneas 2-11 del archivo
actual):

```csharp
@{
    var notificacion = Model.Notificacion;
    ViewData["Titulo"] = "Notificación de precios";
    ViewData["Seccion"] = "precios";
    static bool EsAdvertencia(DetallePrecioApi detalle) =>
        detalle.PrecioActualDocumento is { } actual
        && detalle.PrecioAnteriorEsperado is { } esperado
        && actual != esperado;
    var hayAdvertencias = notificacion.Detalles.Any(EsAdvertencia);
}
```

por:

```csharp
@{
    var notificacion = Model.Notificacion;
    ViewData["Titulo"] = "Notificación de precios";
    ViewData["Seccion"] = "precios";
}
```

Quita el banner de advertencia (busca el bloque que empieza con
`@if (hayAdvertencias)` dentro de la sección "Detalles de precio" y termina
en el `}` que lo cierra):

```razor
    @if (hayAdvertencias)
    {
        <div class="alerta alerta--aviso" role="note">
            <strong>Advertencia:</strong> el «PRECIO ACTUAL» de una o más líneas no
            coincide con el precio vigente esperado a la fecha del documento. Es
            informativo: no impide publicar; revise el borrador si corresponde.
        </div>
    }
```

Elimina ese bloque completo.

En la cabecera de la tabla, quita la columna:

```razor
                    <th scope="col">Precio actual esperado</th>
```

En el cuerpo de la tabla, reemplaza la fila completa:

```razor
                    <tr class="@(EsAdvertencia(detalle) ? "fila--advertencia" : null)">
                        <td>@detalle.TipoAlimento</td>
                        <td>@detalle.Presentacion</td>
                        <td>@detalle.PrecioFinalPor40Kg.ToString("0.00")</td>
                        <td>@(detalle.PrecioActualDocumento?.ToString("0.00") ?? "—")</td>
                        <td>
                            @(detalle.PrecioAnteriorEsperado?.ToString("0.00") ?? "—")
                            @if (EsAdvertencia(detalle))
                            {
                                <span class="advertencia-precio">Diferencia</span>
                            }
                        </td>
                        <td>@(FechasEdad(detalle.EdadDesdeDias, detalle.EdadHastaDias))</td>
                    </tr>
```

por:

```razor
                    <tr>
                        <td>@detalle.TipoAlimento</td>
                        <td>@detalle.Presentacion</td>
                        <td>@detalle.PrecioFinalPor40Kg.ToString("0.00")</td>
                        <td>@(detalle.PrecioActualDocumento?.ToString("0.00") ?? "—")</td>
                        <td>@(FechasEdad(detalle.EdadDesdeDias, detalle.EdadHastaDias))</td>
                    </tr>
```

- [ ] **Step 4: Quitar la columna y la advertencia de `PreciosHuevo/Detalles.cshtml`**

Mismo patrón. Reemplaza el bloque `@{ ... }` del encabezado:

```csharp
@{
    var publicacion = Model.Publicacion;
    ViewData["Titulo"] = "Publicación de precios de huevo";
    ViewData["Seccion"] = "precios-huevo";
    static bool EsAdvertencia(DetallePrecioHuevoApi detalle) =>
        detalle.PrecioActualDocumento is { } actual
        && detalle.PrecioAnteriorEsperado is { } esperado
        && actual != esperado;
    var hayAdvertencias = publicacion.Detalles.Any(EsAdvertencia);
}
```

por:

```csharp
@{
    var publicacion = Model.Publicacion;
    ViewData["Titulo"] = "Publicación de precios de huevo";
    ViewData["Seccion"] = "precios-huevo";
}
```

Elimina el banner:

```razor
    @if (hayAdvertencias)
    {
        <div class="alerta alerta--aviso" role="note">
            <strong>Advertencia:</strong> el «PRECIO ACTUAL» de una o más líneas no
            coincide con el precio anterior esperado. Es informativo: no impide
            publicar; revise el borrador si corresponde.
        </div>
    }
```

Quita la columna de la cabecera:

```razor
                    <th scope="col">Precio actual esperado</th>
```

Reemplaza la fila completa:

```razor
                    <tr class="@(EsAdvertencia(detalle) ? "fila--advertencia" : null)">
                        <td>@detalle.Tamano</td>
                        <td>@detalle.PrecioAlProductor.ToString("0.0000")</td>
                        <td>@(detalle.PrecioActualDocumento?.ToString("0.0000") ?? "—")</td>
                        <td>
                            @(detalle.PrecioAnteriorEsperado?.ToString("0.0000") ?? "—")
                            @if (EsAdvertencia(detalle))
                            {
                                <span class="advertencia-precio">Diferencia</span>
                            }
                        </td>
                        <td>@detalle.PrecioUnitario.ToString("0.0000")</td>
                    </tr>
```

por:

```razor
                    <tr>
                        <td>@detalle.Tamano</td>
                        <td>@detalle.PrecioAlProductor.ToString("0.0000")</td>
                        <td>@(detalle.PrecioActualDocumento?.ToString("0.0000") ?? "—")</td>
                        <td>@detalle.PrecioUnitario.ToString("0.0000")</td>
                    </tr>
```

- [ ] **Step 5: Quitar el CSS huérfano**

En `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css`, elimina
este bloque (y el comentario que lo encabeza):

```css
/* Advertencia informativa de «Precio actual»: no bloquea publicar. */
.fila--advertencia { background-color: var(--bruma); }
.advertencia-precio {
    color: var(--terracota-oscura);
    font-size: 0.8125rem;
    font-weight: 600;
}
```

- [ ] **Step 6: Compilar y ejecutar las pruebas de Trajano.GestorCaisy.Tests**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS. Si algún test referencia `PrecioAnteriorEsperado` fuera de
`ApiIcarusFalsa.cs` (poco probable, pero revisa si falla la compilación),
quítalo siguiendo el mismo criterio del Step 1 de la Task 2.

- [ ] **Step 7: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Detalles.cshtml \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml \
  Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css \
  Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs
git commit -m "refactor(gestorcaisy): quita la columna y advertencia de precio anterior esperado"
```

---

## Task 5: Destacar la publicación vigente en el historial de precios de alimento

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Index.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs`

**Interfaces:**
- Consumes: `NotificacionPreciosResumenApi(Guid Id, DateOnly FechaDocumento, DateOnly VigenteDesde, string Estado, int CantidadDetalles, bool TieneDocumentoOriginal)`
  (ya existe en `ContratosApi.cs`, sin cambios). `FechasDeOficina.Hoy()` (ya
  existe en `Trajano.GestorCaisy.FechasDeOficina`).
- Produces: `VistaHistorialPrecios(IReadOnlyList<NotificacionPreciosResumenApi> Notificaciones, Guid? VigenteId)`
  con factory estática `Crear`.

- [ ] **Step 1: Escribir el test que falla (el Index ahora expone el wrapper con VigenteId)**

En `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs`,
reemplaza el test `IndexDevuelveLaListaDeResumenes` existente:

```csharp
    [Fact]
    public async Task IndexDevuelveLaListaDeResumenes()
    {
        _api.Resumenes.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 12, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsAssignableFrom<IReadOnlyList<NotificacionPreciosResumenApi>>(
            ((ViewResult)vista).Model);
        Assert.Equal(_api.Resumenes.Count, modelo.Count);
    }
```

por estos dos tests:

```csharp
    [Fact]
    public async Task IndexDevuelveLaListaDeResumenes()
    {
        _api.Resumenes.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 12, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPrecios>(((ViewResult)vista).Model);
        Assert.Equal(_api.Resumenes.Count, modelo.Notificaciones.Count);
    }

    [Fact]
    public async Task IndexMarcaComoVigenteLaPublicacionMasRecienteYaIniciada()
    {
        var hoy = FechasDeOficina.Hoy();
        var vigenteId = Guid.NewGuid();
        // Orden descendente por VigenteDesde, igual que ListarHistorialAsync:
        // la vigente es la primera Publicada con vigencia ya iniciada.
        _api.Resumenes.Add(new(
            Guid.NewGuid(), hoy.AddMonths(-2), hoy.AddMonths(1), "Publicada", 10, true));
        _api.Resumenes.Add(new(
            vigenteId, hoy.AddMonths(-1), hoy.AddDays(-5), "Publicada", 10, true));
        _api.Resumenes.Add(new(
            Guid.NewGuid(), hoy.AddMonths(-3), hoy.AddMonths(-2), "Publicada", 10, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPrecios>(((ViewResult)vista).Model);
        Assert.Equal(vigenteId, modelo.VigenteId);
    }

    [Fact]
    public async Task IndexSinNingunaPublicacionVigenteNoMarcaNinguna()
    {
        var hoy = FechasDeOficina.Hoy();
        _api.Resumenes.Add(new(
            Guid.NewGuid(), hoy, hoy.AddMonths(1), "Publicada", 10, true));
        _api.Resumenes.Add(new(
            Guid.NewGuid(), hoy, hoy, "Borrador", 10, false));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPrecios>(((ViewResult)vista).Model);
        Assert.Null(modelo.VigenteId);
    }
```

- [ ] **Step 2: Ejecutar los tests y verificar que fallan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosControllerTests.Index`
Expected: FAIL en compilación — `VistaHistorialPrecios` no existe todavía.

- [ ] **Step 3: Agregar `VistaHistorialPrecios` a `Models/PreciosVistas.cs`**

Agrega esta clase al final de
`Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosVistas.cs`:

```csharp
// Historial de precios de alimento con la publicación vigente destacada
// (spec 2026-10-05): la vigente es la primera Publicada con vigencia ya
// iniciada en una lista ya ordenada por vigencia descendente — el mismo
// criterio que ObtenerVigenteAsync en el backend, sin llamar a la API de
// nuevo.
public sealed record VistaHistorialPrecios(
    IReadOnlyList<NotificacionPreciosResumenApi> Notificaciones, Guid? VigenteId)
{
    public static VistaHistorialPrecios Crear(IReadOnlyList<NotificacionPreciosResumenApi> notificaciones)
    {
        var hoy = FechasDeOficina.Hoy();
        var vigente = notificaciones.FirstOrDefault(
            n => n.Estado == "Publicada" && n.VigenteDesde <= hoy);
        return new VistaHistorialPrecios(notificaciones, vigente?.Id);
    }
}
```

- [ ] **Step 4: Usar el wrapper en `PreciosController.Index`**

Reemplaza en `PreciosController.cs`:

```csharp
    [HttpGet("~/")]
    [HttpGet("~/Precios")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await api.ListarNotificacionesAsync(token));
```

por:

```csharp
    [HttpGet("~/")]
    [HttpGet("~/Precios")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(VistaHistorialPrecios.Crear(await api.ListarNotificacionesAsync(token)));
```

- [ ] **Step 5: Ejecutar los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosControllerTests`
Expected: PASS.

- [ ] **Step 6: Actualizar `Views/Precios/Index.cshtml` para usar el wrapper y destacar la fila**

Reemplaza la línea del modelo:

```razor
@model IReadOnlyList<NotificacionPreciosResumenApi>
```

por:

```razor
@model VistaHistorialPrecios
```

Reemplaza las dos referencias a `Model.Count`/`Model` como lista. El bloque
condicional:

```razor
@if (Model.Count == 0)
```

por:

```razor
@if (Model.Notificaciones.Count == 0)
```

Y el `@foreach`:

```razor
                @foreach (var notificacion in Model)
```

por:

```razor
                @foreach (var notificacion in Model.Notificaciones)
```

En la fila de la tabla, agrega la clase condicional y el chip adicional.
Reemplaza:

```razor
                    <tr>
                        <td>@notificacion.FechaDocumento.ToString("dd/MM/yyyy")</td>
                        <td>@notificacion.VigenteDesde.ToString("dd/MM/yyyy")</td>
                        <td><span class="chip chip--@notificacion.Estado.ToLowerInvariant()">@notificacion.Estado</span></td>
                        <td>@notificacion.CantidadDetalles</td>
                        <td>@(notificacion.TieneDocumentoOriginal ? "Sí" : "—")</td>
                        <td>
                            <a class="enlace" asp-action="Detalles" asp-route-id="@notificacion.Id">Ver</a>
                        </td>
                    </tr>
```

por:

```razor
                    <tr class="@(notificacion.Id == Model.VigenteId ? "fila--vigente" : null)">
                        <td>@notificacion.FechaDocumento.ToString("dd/MM/yyyy")</td>
                        <td>@notificacion.VigenteDesde.ToString("dd/MM/yyyy")</td>
                        <td>
                            <span class="chip chip--@notificacion.Estado.ToLowerInvariant()">@notificacion.Estado</span>
                            @if (notificacion.Id == Model.VigenteId)
                            {
                                <span class="chip chip--vigente-ahora">Vigente ahora</span>
                            }
                        </td>
                        <td>@notificacion.CantidadDetalles</td>
                        <td>@(notificacion.TieneDocumentoOriginal ? "Sí" : "—")</td>
                        <td>
                            <a class="enlace" asp-action="Detalles" asp-route-id="@notificacion.Id">Ver</a>
                        </td>
                    </tr>
```

- [ ] **Step 7: Agregar el CSS nuevo**

En `estilos.css`, debajo del bloque de chips existente (después de la línea
`.chip--rechazado { ... }`), agrega:

```css
/* Publicación vigente destacada en el historial (spec 2026-10-05) */
.fila--vigente { background-color: var(--aqua-claro); border-left: 3px solid var(--aqua); }
.chip--vigente-ahora { background-color: var(--aqua); color: var(--papel); margin-left: 6px; }
```

- [ ] **Step 8: Verificación manual rápida (sin Docker, basta con el sitio levantado)**

Si tienes forma de levantar `Trajano.GestorCaisy` localmente, visita
`/Precios` y confirma visualmente que la fila cuya vigencia ya empezó y es la
más reciente entre las publicadas se ve distinta (tinte + chip "Vigente
ahora"), y que si no hay ninguna vigente no se destaca nada. Si no puedes
levantar el sitio en este entorno, anótalo como pendiente de confirmación
manual en el resumen final — no bloquea el resto del plan.

- [ ] **Step 9: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosVistas.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Index.cshtml \
  Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css \
  Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs
git commit -m "feat(gestorcaisy): destaca la publicacion vigente en el historial de precios de alimento"
```

---

## Task 6: Destacar la publicación vigente en el historial de precios de huevo

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosHuevoVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Index.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs`

**Interfaces:**
- Consumes: `PublicacionPrecioHuevoResumenApi(Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado, int CantidadDetalles, bool TieneDocumentoOriginal)`
  (ya existe, sin cambios). `.fila--vigente` y `.chip--vigente-ahora` (del CSS
  agregado en la Task 5 — se reutilizan tal cual, no hay CSS nuevo en esta
  tarea).
- Produces: `VistaHistorialPreciosHuevo(IReadOnlyList<PublicacionPrecioHuevoResumenApi> Publicaciones, Guid? VigenteId)`
  con factory estática `Crear`.

- [ ] **Step 1: Mirar primero `PreciosHuevoControllerTests.cs` para replicar el patrón exacto del test `Index` existente**

Abre `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs`
y localiza el test que ejercita `Index` (debería llamarse de forma similar a
`IndexDevuelveLaListaDeResumenes`, verificando el tipo del `Model` contra
`IReadOnlyList<PublicacionPrecioHuevoResumenApi>`). Vas a reemplazarlo
siguiendo exactamente el mismo patrón que la Task 5, Step 1, pero con los
nombres de este dominio.

- [ ] **Step 2: Escribir los tests que fallan**

Reemplaza ese test por estos tres (ajusta el nombre del campo de la API
falsa si en este archivo se llama distinto a `_api.ResumenesHuevo` —
confirmado en `ApiIcarusFalsa.cs:146` que la propiedad es
`ResumenesHuevo`):

```csharp
    [Fact]
    public async Task IndexDevuelveLaListaDeResumenes()
    {
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 2, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPreciosHuevo>(((ViewResult)vista).Model);
        Assert.Equal(_api.ResumenesHuevo.Count, modelo.Publicaciones.Count);
    }

    [Fact]
    public async Task IndexMarcaComoVigenteLaPublicacionMasRecienteYaIniciada()
    {
        var hoy = FechasDeOficina.Hoy();
        var vigenteId = Guid.NewGuid();
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), hoy.AddMonths(-2), hoy.AddMonths(1), "Publicada", 2, true));
        _api.ResumenesHuevo.Add(new(
            vigenteId, hoy.AddMonths(-1), hoy.AddDays(-5), "Publicada", 2, true));
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), hoy.AddMonths(-3), hoy.AddMonths(-2), "Publicada", 2, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPreciosHuevo>(((ViewResult)vista).Model);
        Assert.Equal(vigenteId, modelo.VigenteId);
    }

    [Fact]
    public async Task IndexSinNingunaPublicacionVigenteNoMarcaNinguna()
    {
        var hoy = FechasDeOficina.Hoy();
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), hoy, hoy.AddMonths(1), "Publicada", 2, true));
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), hoy, hoy, "Borrador", 2, false));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaHistorialPreciosHuevo>(((ViewResult)vista).Model);
        Assert.Null(modelo.VigenteId);
    }
```

Si el test original que reemplazaste usaba otro nombre de método, mantén el
nombre `IndexDevuelveLaListaDeResumenes` igual para no romper referencias
externas (no debería haber ninguna, pero revisa antes de renombrar).

- [ ] **Step 3: Ejecutar los tests y verificar que fallan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosHuevoControllerTests.Index`
Expected: FAIL en compilación — `VistaHistorialPreciosHuevo` no existe.

- [ ] **Step 4: Agregar `VistaHistorialPreciosHuevo` a `Models/PreciosHuevoVistas.cs`**

Agrega al final del archivo:

```csharp
// Historial de precios de huevo con la publicación vigente destacada (spec
// 2026-10-05): mismo criterio que VistaHistorialPrecios para alimento.
public sealed record VistaHistorialPreciosHuevo(
    IReadOnlyList<PublicacionPrecioHuevoResumenApi> Publicaciones, Guid? VigenteId)
{
    public static VistaHistorialPreciosHuevo Crear(
        IReadOnlyList<PublicacionPrecioHuevoResumenApi> publicaciones)
    {
        var hoy = FechasDeOficina.Hoy();
        var vigente = publicaciones.FirstOrDefault(
            p => p.Estado == "Publicada" && p.FechaVigencia <= hoy);
        return new VistaHistorialPreciosHuevo(publicaciones, vigente?.Id);
    }
}
```

- [ ] **Step 5: Usar el wrapper en `PreciosHuevoController.Index`**

Reemplaza:

```csharp
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await api.ListarPublicacionesHuevoAsync(token));
```

por:

```csharp
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(VistaHistorialPreciosHuevo.Crear(await api.ListarPublicacionesHuevoAsync(token)));
```

- [ ] **Step 6: Ejecutar los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosHuevoControllerTests`
Expected: PASS.

- [ ] **Step 7: Actualizar `Views/PreciosHuevo/Index.cshtml`**

Reemplaza:

```razor
@model IReadOnlyList<PublicacionPrecioHuevoResumenApi>
```

por:

```razor
@model VistaHistorialPreciosHuevo
```

Reemplaza:

```razor
@if (Model.Count == 0)
```

por:

```razor
@if (Model.Publicaciones.Count == 0)
```

Reemplaza:

```razor
                @foreach (var publicacion in Model)
```

por:

```razor
                @foreach (var publicacion in Model.Publicaciones)
```

Reemplaza la fila completa:

```razor
                    <tr>
                        <td>@publicacion.FechaNotificacion.ToString("dd/MM/yyyy")</td>
                        <td>@publicacion.FechaVigencia.ToString("dd/MM/yyyy")</td>
                        <td><span class="chip chip--@publicacion.Estado.ToLowerInvariant()">@publicacion.Estado</span></td>
                        <td>@publicacion.CantidadDetalles</td>
                        <td>@(publicacion.TieneDocumentoOriginal ? "Sí" : "—")</td>
                        <td>
                            <a class="enlace" asp-action="Detalles" asp-route-id="@publicacion.Id">Ver</a>
                        </td>
                    </tr>
```

por:

```razor
                    <tr class="@(publicacion.Id == Model.VigenteId ? "fila--vigente" : null)">
                        <td>@publicacion.FechaNotificacion.ToString("dd/MM/yyyy")</td>
                        <td>@publicacion.FechaVigencia.ToString("dd/MM/yyyy")</td>
                        <td>
                            <span class="chip chip--@publicacion.Estado.ToLowerInvariant()">@publicacion.Estado</span>
                            @if (publicacion.Id == Model.VigenteId)
                            {
                                <span class="chip chip--vigente-ahora">Vigente ahora</span>
                            }
                        </td>
                        <td>@publicacion.CantidadDetalles</td>
                        <td>@(publicacion.TieneDocumentoOriginal ? "Sí" : "—")</td>
                        <td>
                            <a class="enlace" asp-action="Detalles" asp-route-id="@publicacion.Id">Ver</a>
                        </td>
                    </tr>
```

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosHuevoVistas.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Index.cshtml \
  Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs
git commit -m "feat(gestorcaisy): destaca la publicacion vigente en el historial de precios de huevo"
```

---

## Task 7: Cierre — puerta de calidad completa y entrega

**Files:** ninguno nuevo; esta tarea solo ejecuta y verifica.

- [ ] **Step 1: Ejecutar la puerta de calidad completa**

Run: `./verify.ps1` (Windows) desde la raíz del repo. Requiere Docker
corriendo (hay pruebas de integración con Testcontainers.MsSql desde la
Task 1, Step 8). Debe quedar verde. Si falla, corrige la causa — nunca el
gate — y vuelve a correrlo antes de continuar.

- [ ] **Step 2: Revisar el diff completo antes de entregar**

Run: `git log --oneline -7` y `git diff --stat 198bb14..HEAD` (ajusta el
commit base si el historial avanzó) para confirmar que los 6 commits de este
plan son los únicos cambios nuevos y que no quedó nada sin commitear.
Run: `git status --short` y confirma que no hay cambios pendientes.

- [ ] **Step 3: Resumen final para el usuario**

No hagas `git push`: deja el resumen explícito de qué se hizo (los 6
commits, uno por tarea), qué gate corrió y con qué resultado, y si el Step 8
de la Task 5 (verificación visual manual) se pudo hacer en este entorno o
quedó pendiente para que el usuario la confirme en un navegador. El `git
push` requiere confirmación explícita del usuario antes de ejecutarse.
