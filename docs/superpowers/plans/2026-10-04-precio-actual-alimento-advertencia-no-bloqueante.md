# Precio actual de alimento: advertencia no bloqueante (igualar a huevo) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quitar el bloqueo de publicación por discrepancia de la columna «Precio actual» en precios de alimento y dejarlo como advertencia informativa no bloqueante, exactamente como ya funciona en precios de huevo.

**Architecture:** El módulo `GestionAvicola` tiene dos features paralelas y ya desacopladas: `PreciosAlimentos` y `PreciosHuevo`. Huevo ya resolvió este mismo problema (spec `2026-09-15-feedback-precios-huevo-design.md`): el chequeo nunca bloquea `Publicar()`; en su lugar, las consultas (`ObtenerPublicacionPrecioHuevoHandler`, `ObtenerPrecioHuevoVigenteHandler`) calculan un campo informativo `PrecioAnteriorEsperado` comparando el detalle contra la publicación vigente a la fecha del documento, y el MVC lo muestra como advertencia visual (`fila--advertencia`, `alerta--aviso`) sin impedir publicar. Este plan replica ese mismo patrón, campo por campo, para `PreciosAlimentos`.

**Tech Stack:** .NET (MediatR, FluentValidation, EF Core) en `Icarus.GestionAvicola.Application`/`.Domain`; ASP.NET Core MVC Razor en `Trajano.GestorCaisy`; xUnit + NSubstitute para pruebas.

## Global Constraints

- Español neutro sin voseo en todo texto de interfaz, comentarios y mensajes de commit.
- UTF-8 sin BOM, sin mojibake, con acentos correctos.
- No registrar PII; los registros de vuelo (`registroVuelo.Decidir`) nunca llevan precios.
- `./verify.ps1` (o `./verify.sh`) obligatorio antes de cada commit y push; prohibido `--no-verify`.
- TDD estricto: cada test nuevo debe verse fallar antes de implementar.
- Rama `develop`, sin pull requests; `git push` solo tras confirmación explícita del usuario (pedir confirmación antes de ejecutar el push final).
- No tocar la feature de precios de huevo: solo se usa como referencia de patrón, su código no cambia.
- No ampliar el alcance a los pedidos de alimento ni a `ObtenerPrecioVigenteQuery` consumido por otros módulos más allá de agregar el campo opcional.

## File Structure

Archivos que este plan modifica (ninguno se crea):

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs` — quita el bloqueo de `Publicar`, agrega `PrecioAnteriorEsperado` a la consulta.
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs` — quita pruebas del bloqueo, agrega pruebas del campo informativo.
- `Icarus/tests/Icarus.IntegrationTests/PreciosAlimentosEndpointsTests.cs` — reemplaza la prueba de bloqueo por una de éxito con advertencia.
- `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs` — agrega `PrecioAnteriorEsperado` a `DetallePrecioApi`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs` — prueba de parseo del campo nuevo.
- `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs` — quita el manejo especial de discrepancias por detalle.
- `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/ConfirmarPublicacion.cshtml` — quita la alerta bloqueante.
- `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Detalles.cshtml` — agrega la advertencia visual no bloqueante.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs` — fixture con un caso de diferencia y uno de coincidencia.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs` — quita la prueba del manejo especial retirado.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs` — quita la prueba de bloqueo, agrega la de advertencia en `Detalles`.
- `docs/dominio/glosario-avicola.md` — actualiza la definición para que ya no contraste alimento con huevo.

No se crean archivos nuevos: huevo ya probó el patrón, alimento solo lo adopta con sus propios tipos (`TipoAlimento`/`PresentacionAlimento` en vez de `TamanoHuevo`).

---

### Task 1: Quitar el bloqueo de publicación por discrepancia de «Precio actual»

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioNotificacionesPrecios.ObtenerVigenteAsync(DateOnly, CancellationToken)` (ya existe, se sigue usando en Task 2 para la consulta, no en `Publicar`).
- Produces: `PublicarNotificacionPreciosHandler.Handle` ya no lanza `ValidationException` por discrepancia de `PrecioActualDocumento`.

- [ ] **Step 1: Borrar las pruebas que exigen el bloqueo (deben desaparecer antes de tocar el handler, para que la suite no quede verificando un comportamiento que se va a eliminar)**

En `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`, borra completos estos tres métodos:
  - `PublicarRechazaUnPrecioActualDiscrepanteConLaVigencia`
  - `PublicarDevuelveUnaDiscrepanciaPorCadaPrecioActualDiferente`
  - `PublicarControlaElPrecioActualContraLaPublicacionDeLaFechaDelDocumento`

Borra también el helper que solo usaban esos tres tests:

```csharp
    private static NotificacionPreciosAlimentos VigentePublicada() =>
        new(new(2025, 10, 1), new(2025, 10, 1), 1.10m, 0.50m, 0.70m,
            [new DatosDetallePrecio(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m, 22, 35, null)]);
```

Simplifica `DoceDetalles` quitando el parámetro que ya nadie varía (antes se usaba `DoceDetalles(179m)` solo en el test borrado):

```csharp
    private static IReadOnlyList<DatosDetallePrecio> DoceDetalles() =>
        Enum.GetValues<TipoAlimento>()
            .SelectMany(t => new[]
                {
                    new DatosDetallePrecio(t, PresentacionAlimento.Bolsa, 176.5m, 22, 35, 180m),
                    new DatosDetallePrecio(t, PresentacionAlimento.Granel, 174.5m, 22, 35, 180m),
                })
            .ToList();
```

Actualiza el comentario de cabecera de la clase (antes decía que la discrepancia bloqueaba):

```csharp
// SP8A Tarea 3 (spec: "Importación del PDF" y "Modelo"): el borrador se importa
// y se publica con confirmación explícita; la columna «Precio actual»
// discrepante es solo informativa y no bloquea publicar (spec 2026-09-15,
// alineado con precios de huevo); no pueden coexistir dos publicaciones
// activas con la misma vigencia. Los errores de formato rechazan la
// importación completa sin persistencia parcial.
public class PreciosAlimentosHandlerTests
```

- [ ] **Step 2: Ejecutar la suite para confirmar que compila y pasa sin los tres tests**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: PASS (todos los tests restantes de esa clase, incluido `PublicarConControlCoherenteSellaElBorrador`, que seguirá pasando porque su stub de `ObtenerVigenteAsync` simplemente deja de ser necesario, sin que eso rompa nada).

- [ ] **Step 3: Quitar el bloqueo del handler**

En `ComandosPreciosAlimentos.cs`, reemplaza el bloque completo de `PublicarNotificacionPreciosHandler` (incluye su comentario, el método `BuscarDiscrepancias`, el campo `CulturaInterfaz` y el record `DiscrepanciaPrecio`):

```csharp
// La publicación exige confirmación explícita (spec SP8): unicidad de
// vigencia activa, control de la columna «Precio actual» contra la vigente y
// sellado inmediato del borrador. Nunca se publica automáticamente.
public sealed class PublicarNotificacionPreciosHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarNotificacionPreciosCommand>
{
    public async Task Handle(PublicarNotificacionPreciosCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        if (await repositorio.ExistePublicadaConVigenciaIgualAsync(
                notificacion.VigenteDesde, notificacion.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");

        var vigente = await repositorio.ObtenerVigenteAsync(notificacion.FechaDocumento, cancellationToken);
        var discrepancias = BuscarDiscrepancias(notificacion, vigente);
        if (discrepancias.Count > 0)
            throw new ValidationException(discrepancias.Select(d => new ValidationFailure(
                $"Detalles[{d.DetalleId}].PrecioActualDocumento", d.Mensaje())));

        notificacion.Publicar();
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.precios.publicar",
                ("CantidadDetalles", DatoRegistroVuelo.Entero)),
            "publicacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = notificacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // La interfaz de CAISY es en español (Bolivia): los precios de la
    // discrepancia se muestran con coma decimal.
    private static readonly CultureInfo CulturaInterfaz = CultureInfo.GetCultureInfo("es-BO");

    private static List<DiscrepanciaPrecio> BuscarDiscrepancias(
        NotificacionPreciosAlimentos notificacion, NotificacionPreciosAlimentos? vigente)
    {
        if (vigente is null)
            return [];
        var preciosVigentes = vigente.Detalles.ToDictionary(
            d => (d.TipoAlimento, d.Presentacion), d => d.PrecioFinalPor40Kg);
        return notificacion.Detalles
            .Where(d => d.PrecioActualDocumento is { } precioActual
                && preciosVigentes.TryGetValue((d.TipoAlimento, d.Presentacion), out var precioVigente)
                && precioActual != precioVigente)
            .Select(d => new DiscrepanciaPrecio(
                d.Id, d.TipoAlimento, d.Presentacion, d.PrecioActualDocumento!.Value,
                preciosVigentes[(d.TipoAlimento, d.Presentacion)]))
            .ToList();
    }

    // El borrador es editable: la discrepancia se identifica por tipo y
    // presentación, nunca por la fila original del Excel (spec 2026-09-15).
    private sealed record DiscrepanciaPrecio(
        Guid DetalleId, TipoAlimento Tipo, PresentacionAlimento Presentacion,
        decimal PrecioBorrador, decimal PrecioVigente)
    {
        public string Mensaje() =>
            $"Tipo: {Tipo}; presentación: {Presentacion}; columna: PRECIO ACTUAL; " +
            $"valor del borrador: {PrecioBorrador.ToString("0.00", CulturaInterfaz)}; " +
            $"valor vigente esperado: {PrecioVigente.ToString("0.00", CulturaInterfaz)}.";
    }
}
```

con esto:

```csharp
// La publicación exige confirmación explícita (spec SP8): unicidad de
// vigencia activa y sellado inmediato del borrador. La columna «Precio
// actual» ya no bloquea publicar (spec 2026-09-15, alineado con precios de
// huevo): queda solo como advertencia informativa en la consulta (ver
// ObtenerNotificacionPreciosHandler / ObtenerPrecioVigenteHandler más abajo).
// Nunca se publica automáticamente.
public sealed class PublicarNotificacionPreciosHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarNotificacionPreciosCommand>
{
    public async Task Handle(PublicarNotificacionPreciosCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        if (await repositorio.ExistePublicadaConVigenciaIgualAsync(
                notificacion.VigenteDesde, notificacion.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");

        notificacion.Publicar();
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.precios.publicar",
                ("CantidadDetalles", DatoRegistroVuelo.Entero)),
            "publicacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = notificacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

Quita también el `using System.Globalization;` del principio del archivo: tras borrar `CulturaInterfaz` ya no queda ninguna referencia a `CultureInfo` en el archivo. Deja intacto `using FluentValidation.Results;` (lo siguen usando `ImportarNotificacionPdfHandler` e `ImportarNotificacionExcelHandler` para sus propios `ValidationFailure`).

- [ ] **Step 4: Ejecutar la suite para confirmar que todo compila y pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs
git commit -m "fix(precios-alimentos): quita el bloqueo de publicacion por discrepancia de precio actual"
```

---

### Task 2: Exponer `PrecioAnteriorEsperado` en las consultas de alimento

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioNotificacionesPrecios.ObtenerVigenteAsync(DateOnly fecha, CancellationToken)` (ya existe).
- Produces: `DetallePrecioResumen` gana una propiedad `decimal? PrecioAnteriorEsperado = null` al final (último parámetro, para no romper los `new DetallePrecioResumen(...)` posicionales existentes). `MapeadorPrecios.Mapear` gana un segundo parámetro opcional `NotificacionPreciosAlimentos? anterior = null`.

- [ ] **Step 1: Escribir las pruebas que fallan, mirando exactamente el patrón ya probado en huevo (`PreciosHuevoHandlerTests.ObtenerDetalleExponeElPrecioAnteriorEsperadoPorTamano` y sus dos pares negativos)**

Agrega estos tres métodos a `PreciosAlimentosHandlerTests.cs` (junto a los demás, antes del cierre de la clase):

```csharp
    [Fact]
    public async Task ObtenerDetalleExponeElPrecioAnteriorEsperadoPorTipoYPresentacion()
    {
        var borrador = new NotificacionPreciosAlimentos(
            FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
            [new DatosDetallePrecio(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 176.5m, 22, 35, 179m)]);
        var vigente = new NotificacionPreciosAlimentos(
            new(2025, 10, 1), new(2025, 10, 1), 1.10m, 0.50m, 0.70m,
            [new DatosDetallePrecio(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m, 22, 35, null)]);
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ObtenerVigenteAsync(borrador.FechaDocumento, Arg.Any<CancellationToken>())
            .Returns(vigente);

        var detalle = await new ObtenerNotificacionPreciosHandler(_repositorio).Handle(
            new ObtenerNotificacionPreciosQuery(borrador.Id), CancellationToken.None);

        var fila = Assert.Single(detalle.Detalles);
        Assert.Equal(180m, fila.PrecioAnteriorEsperado);
    }

    [Fact]
    public async Task ObtenerDetalleSinPublicacionAnteriorNoInventaPrecioEsperado()
    {
        var borrador = CrearBorradorPublicable();
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ObtenerVigenteAsync(borrador.FechaDocumento, Arg.Any<CancellationToken>())
            .Returns((NotificacionPreciosAlimentos?)null);

        var detalle = await new ObtenerNotificacionPreciosHandler(_repositorio).Handle(
            new ObtenerNotificacionPreciosQuery(borrador.Id), CancellationToken.None);

        Assert.All(detalle.Detalles, d => Assert.Null(d.PrecioAnteriorEsperado));
    }

    [Fact]
    public async Task ObtenerDetalleConTipoOPresentacionSinCorrespondenciaNoInventaPrecioEsperado()
    {
        var borrador = CrearBorradorPublicable();
        var vigente = new NotificacionPreciosAlimentos(
            new(2025, 10, 1), new(2025, 10, 1), 1.10m, 0.50m, 0.70m,
            [new DatosDetallePrecio(TipoAlimento.Iniciador, PresentacionAlimento.Granel, 180m, 22, 35, null)]);
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ObtenerVigenteAsync(borrador.FechaDocumento, Arg.Any<CancellationToken>())
            .Returns(vigente);

        var detalle = await new ObtenerNotificacionPreciosHandler(_repositorio).Handle(
            new ObtenerNotificacionPreciosQuery(borrador.Id), CancellationToken.None);

        // CrearBorradorPublicable no tiene ninguna línea Iniciador/Granel: la
        // vigente no corresponde a ningún tipo+presentación del borrador.
        Assert.All(detalle.Detalles, d => Assert.Null(d.PrecioAnteriorEsperado));
    }
```

- [ ] **Step 2: Ejecutar los tests nuevos y confirmar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: FAIL (no existe `fila.PrecioAnteriorEsperado`; error de compilación porque `DetallePrecioResumen` todavía no tiene esa propiedad)

- [ ] **Step 3: Agregar el campo y el cálculo**

En `ComandosPreciosAlimentos.cs`, cambia el record:

```csharp
public sealed record DetallePrecioResumen(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias, string Codigo);
```

por:

```csharp
// PrecioAnteriorEsperado es informativo (spec 2026-09-15, alineado con
// precios de huevo): el PrecioFinalPor40Kg de la publicación vigente a la
// fecha del documento para el mismo tipo y presentación. No se persiste ni
// bloquea publicar; solo alimenta la advertencia visual.
public sealed record DetallePrecioResumen(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias, string Codigo,
    decimal? PrecioAnteriorEsperado = null);
```

Cambia los dos handlers de consulta:

```csharp
public sealed class ObtenerNotificacionPreciosHandler(IRepositorioNotificacionesPrecios repositorio)
    : IRequestHandler<ObtenerNotificacionPreciosQuery, NotificacionPreciosDetalle>
{
    public async Task<NotificacionPreciosDetalle> Handle(
        ObtenerNotificacionPreciosQuery request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        var anterior = await repositorio.ObtenerVigenteAsync(notificacion.FechaDocumento, cancellationToken);
        return MapeadorPrecios.Mapear(notificacion, anterior);
    }
}

// Fecha por defecto: hoy en Bolivia (spec SP8); la consulta se usa al preparar
// pedidos y al comparar contra el documento.
public sealed class ObtenerPrecioVigenteHandler(IRepositorioNotificacionesPrecios repositorio)
    : IRequestHandler<ObtenerPrecioVigenteQuery, NotificacionPreciosDetalle?>
{
    public async Task<NotificacionPreciosDetalle?> Handle(
        ObtenerPrecioVigenteQuery request, CancellationToken cancellationToken)
    {
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        if (vigente is null)
            return null;
        var anterior = await repositorio.ObtenerVigenteAsync(vigente.FechaDocumento, cancellationToken);
        return MapeadorPrecios.Mapear(vigente, anterior);
    }
}
```

Y el mapeador:

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

- [ ] **Step 4: Ejecutar los tests y confirmar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests`
Expected: PASS (todos, incluidos los tres nuevos)

- [ ] **Step 5: Ejecutar toda la suite de `Icarus.UnitTests` para descartar roturas colaterales**

Run: `dotnet test Icarus/tests/Icarus.UnitTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs
git commit -m "feat(precios-alimentos): expone PrecioAnteriorEsperado como advertencia informativa"
```

---

### Task 3: Actualizar la prueba de integración de la API

**Files:**
- Modify: `Icarus/tests/Icarus.IntegrationTests/PreciosAlimentosEndpointsTests.cs`

**Interfaces:**
- Consumes: endpoints HTTP existentes `POST /api/precios-alimentos/{id}/publicar` y `GET /api/precios-alimentos/{id}` (sin cambios de contrato salvo el campo nuevo `precioAnteriorEsperado` en cada línea de `detalles`).

Este test requiere Docker corriendo (Testcontainers.MsSql), como el resto de `Icarus.IntegrationTests`.

- [ ] **Step 1: Reescribir el test que hoy espera el bloqueo para que, en cambio, verifique éxito + advertencia**

Reemplaza el método completo `ElPrecioActualDiscrepanteBloqueaLaPublicacion` por:

```csharp
    [Fact]
    public async Task ElPrecioActualDiscrepanteYaNoBloqueaYQuedaComoAdvertencia()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();

        // Publicación base: rige desde antes de la fecha del segundo documento,
        // de modo que la columna «Precio actual» del segundo tenga contra qué
        // compararse. (176.50 es el precio nuevo del documento base.)
        var baseVigente = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoControl, VigenciaBaseline));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, baseVigente));

        var borrador = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoControl, VigenciaUnica()));
        var respuesta = await PublicarAsync(cliente, token, borrador);

        // El chequeo ya no bloquea (spec 2026-09-15, alineado con precios de
        // huevo): publicar tiene éxito aunque la columna «Precio actual» no
        // coincida con la vigente a la fecha del documento.
        Assert.Equal(HttpStatusCode.NoContent, respuesta);
        var detalle = await ObtenerAsync(cliente, token, $"/api/precios-alimentos/{borrador}");
        Assert.Equal("Publicada", detalle.GetProperty("estado").GetString());
        var linea = detalle.GetProperty("detalles").EnumerateArray().First();
        Assert.True(linea.TryGetProperty("precioAnteriorEsperado", out var anterior));
        Assert.Equal(176.50m, anterior.GetDecimal());
    }
```

- [ ] **Step 2: Ejecutar el test y confirmar que falla si el chequeo todavía bloqueara (control de regresión)**

Como Task 1 ya se aplicó, este paso en la práctica confirma éxito directo; si se ejecuta este plan en orden, usa este Run solo para verificar el resultado esperado:

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~ElPrecioActualDiscrepanteYaNoBloqueaYQuedaComoAdvertencia`
Expected: PASS (requiere Docker corriendo)

- [ ] **Step 3: Ejecutar toda la clase para descartar roturas colaterales**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~PreciosAlimentosEndpointsTests`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/PreciosAlimentosEndpointsTests.cs
git commit -m "test(precios-alimentos): verifica que la discrepancia ya no bloquea publicar"
```

---

### Task 4: Contrato MVC — agregar `PrecioAnteriorEsperado` a `DetallePrecioApi`

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs`

**Interfaces:**
- Produces: `DetallePrecioApi` gana `decimal? PrecioAnteriorEsperado = null` al final (último parámetro posicional, igual que `DetallePrecioHuevoApi` ya lo tiene).

- [ ] **Step 1: Escribir el test de parseo que falla, mirando el equivalente de huevo (`ObtenerPublicacionHuevoParseaElPrecioAnteriorEsperado`)**

Agrega a `ApiIcarusClientTests.cs`:

```csharp
    [Fact]
    public async Task ObtenerNotificacionParseaElPrecioAnteriorEsperado()
    {
        var id = Guid.NewGuid();
        _manejador.Responder(HttpStatusCode.OK,
            $$"""
            {"id":"{{id}}","fechaDocumento":"2025-11-02","vigenteDesde":"2025-12-01",
             "estado":"Borrador","aporteCaisy":1.20,"fondo":0.60,"servicios":0.75,
             "documentoOriginalId":null,
             "detalles":[{"id":"11111111-1111-1111-1111-111111111111",
                         "tipoAlimento":"Iniciador","presentacion":"Bolsa",
                         "precioFinalPor40Kg":176.50,"precioActualDocumento":179.00,
                         "edadDesdeDias":22,"edadHastaDias":35,
                         "precioAnteriorEsperado":180.00}]}
            """);

        var notificacion = await _cliente.ObtenerNotificacionAsync(id);

        var detalle = Assert.Single(notificacion.Detalles);
        Assert.Equal(180.00m, detalle.PrecioAnteriorEsperado);
        Assert.Equal($"{BaseApi}precios-alimentos/{id}", _manejador.Peticiones[0].Uri.ToString());
    }
```

- [ ] **Step 2: Ejecutar y confirmar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~ObtenerNotificacionParseaElPrecioAnteriorEsperado`
Expected: FAIL (`DetallePrecioApi` no tiene `PrecioAnteriorEsperado`; error de compilación)

- [ ] **Step 3: Agregar el campo**

En `ContratosApi.cs`, cambia:

```csharp
public sealed record DetallePrecioApi(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias);
```

por:

```csharp
// PrecioAnteriorEsperado (spec 2026-09-15, alineado con precios de huevo) es
// el PrecioFinalPor40Kg vigente a la fecha del documento para el mismo tipo y
// presentación; solo alimenta la advertencia visual y nunca bloquea publicar.
public sealed record DetallePrecioApi(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias,
    decimal? PrecioAnteriorEsperado = null);
```

No se toca `ApiIcarusClient.cs`: `ObtenerNotificacionAsync` ya deserializa con `ReadFromJsonAsync<NotificacionPreciosDetalleApi>` y System.Text.Json completa la propiedad nueva automáticamente, igual que ya ocurre con `DetallePrecioHuevoApi`.

- [ ] **Step 4: Ejecutar el test y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~ObtenerNotificacionParseaElPrecioAnteriorEsperado`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs
git commit -m "feat(gestor-caisy): parsea PrecioAnteriorEsperado de la notificacion de precios"
```

---

### Task 5: Simplificar el controlador MVC (quitar el manejo especial de discrepancias por detalle)

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs`

**Interfaces:**
- Produces: la acción `Publicar` (POST) ya no escribe `TempData["ErroresDiscrepancias"]`; cualquier error 400/409 de la API pasa a `TempData["Error"]` igual que cualquier otro error, usando `error.MensajeParaLaInterfaz()`.

- [ ] **Step 1: Borrar la prueba que exige el manejo especial retirado**

En `PreciosControllerTests.cs`, borra completo el método `PublicarConVariasDiscrepanciasConservaTodosLosMensajesParaLaConfirmacion`. No toques `PublicarConDiscrepanciaRegresaAConfirmarConElError`: sigue siendo válida sin cambios, porque ya verifica el camino genérico (`TempData["Error"]`) con una clave `"Documento"` que nunca pasó por el branch que se retira.

- [ ] **Step 2: Ejecutar la clase y confirmar que compila y pasa sin ese test**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosControllerTests`
Expected: PASS

- [ ] **Step 3: Simplificar el controlador**

En `PreciosController.cs`, reemplaza:

```csharp
    [HttpPost("{id:guid}/Publicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.PublicarAsync(id, token);
            TempData["Exito"] = "La notificación quedó publicada.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            // Una discrepancia de «Precio actual» llega como una entrada de
            // validación por línea: se conservan todas para la pantalla de
            // confirmación en lugar de aplanarlas en una sola alerta. Los 409 y
            // las validaciones ajenas a los detalles mantienen su mensaje.
            if (error.Estado == StatusCodes.Status400BadRequest
                && TieneDiscrepanciasDeDetalles(error.ErroresValidacion))
                TempData[ClaveErroresDiscrepancias] = error.ErroresValidacion!
                    .SelectMany(entrada => entrada.Value)
                    .ToList();
            else
                TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(ConfirmarPublicacion), new { id });
        }
    }

    private const string ClaveErroresDiscrepancias = "ErroresDiscrepancias";

    private static bool TieneDiscrepanciasDeDetalles(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errores) =>
        errores is not null && errores.Keys.Any(
            clave => clave.StartsWith("Detalles[", StringComparison.Ordinal));
```

por:

```csharp
    [HttpPost("{id:guid}/Publicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.PublicarAsync(id, token);
            TempData["Exito"] = "La notificación quedó publicada.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(ConfirmarPublicacion), new { id });
        }
    }
```

- [ ] **Step 4: Ejecutar la clase completa y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosControllerTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs
git commit -m "refactor(gestor-caisy): quita el manejo especial de discrepancias al publicar precios de alimento"
```

---

### Task 6: Vista «Confirmar la publicación» — quitar la alerta bloqueante

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/ConfirmarPublicacion.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs`

**Interfaces:**
- Consumes: ninguno nuevo. La vista deja de leer `TempData["ErroresDiscrepancias"]`.

- [ ] **Step 1: Borrar la prueba que exige la alerta retirada**

En `FlujoPreciosTests.cs`, borra completo el método `VariasDiscrepanciasAlPublicarSeMuestranTodasEnLaConfirmacion`.

- [ ] **Step 2: Ejecutar la clase y confirmar que compila y pasa sin ese test**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~FlujoPreciosTests`
Expected: PASS

- [ ] **Step 3: Quitar el bloque de la vista**

En `ConfirmarPublicacion.cshtml`, borra este bloque completo (queda justo después de `<partial name="_Avisos" />`):

```cshtml
@if (TempData["ErroresDiscrepancias"] is IEnumerable<string> discrepancias)
{
    <section class="alerta alerta--error" role="alert">
        <p>
            <strong>No se pudo publicar: el «Precio actual» no coincide con la
            publicación vigente.</strong>
            Corrija las líneas señaladas en el borrador antes de reintentar.
        </p>
        <ul class="validation-summary-errors">
            @foreach (var discrepancia in discrepancias)
            {
                <li>@discrepancia</li>
            }
        </ul>
    </section>
}
```

El resto del archivo (resumen, tabla de detalles, botón «Publicar ahora») queda igual: ya coincide con el patrón de `Views/PreciosHuevo/ConfirmarPublicacion.cshtml`, que nunca tuvo esta alerta.

- [ ] **Step 4: Ejecutar la suite de MVC completa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/ConfirmarPublicacion.cshtml Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs
git commit -m "fix(gestor-caisy): quita la alerta bloqueante de precio actual al confirmar la publicacion"
```

---

### Task 7: Vista «Detalles» — agregar la advertencia visual no bloqueante

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Detalles.cshtml`
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs`

**Interfaces:**
- Consumes: `DetallePrecioApi.PrecioAnteriorEsperado` (Task 4).
- Produces: ninguno nuevo; solo markup.

No se necesita CSS nuevo: `.fila--advertencia` y `.advertencia-precio` ya existen en `wwwroot/css/estilos.css` (las usa `Views/PreciosHuevo/Detalles.cshtml`) y son compartidas entre features.

- [ ] **Step 1: Actualizar el fixture compartido para tener un caso de diferencia y uno de coincidencia**

En `ApiIcarusFalsa.cs`, dentro de `CrearDetalle`, cambia:

```csharp
    public static NotificacionPreciosDetalleApi CrearDetalle(
        Guid id, string estado = "Borrador", string vigenteDesde = "2025-12-01") =>
        new(
            id, new(2025, 11, 2), DateOnly.Parse(vigenteDesde, CultureInfo.InvariantCulture), estado,
            1.20m, 0.60m, 0.75m, Guid.NewGuid(),
            [
                new DetallePrecioApi(
                    Guid.NewGuid(), "Preiniciador", "Bolsa", 118.50m, 115.00m, 1, 21),
                new DetallePrecioApi(
                    Guid.NewGuid(), "PosturaDos", "Granel", 112.75m, 110.25m, null, null),
            ]);
```

por:

```csharp
    public static NotificacionPreciosDetalleApi CrearDetalle(
        Guid id, string estado = "Borrador", string vigenteDesde = "2025-12-01") =>
        new(
            id, new(2025, 11, 2), DateOnly.Parse(vigenteDesde, CultureInfo.InvariantCulture), estado,
            1.20m, 0.60m, 0.75m, Guid.NewGuid(),
            [
                // La fila Preiniciador difiere del precio anterior esperado y la
                // fila PosturaDos coincide, para probar ambos casos visuales.
                new DetallePrecioApi(
                    Guid.NewGuid(), "Preiniciador", "Bolsa", 118.50m, 115.00m, 1, 21, 117.00m),
                new DetallePrecioApi(
                    Guid.NewGuid(), "PosturaDos", "Granel", 112.75m, 110.25m, null, null, 110.25m),
            ]);
```

- [ ] **Step 2: Escribir el test de integración que falla, mirando el equivalente de huevo (`FlujoPreciosHuevoTests.DetallesAdvierteDiferenciasDePrecioActualSinBloquearPublicar`)**

Agrega a `FlujoPreciosTests.cs`:

```csharp
    [Fact]
    public async Task DetallesAdvierteDiferenciasDePrecioActualSinBloquearPublicar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var html = await cliente.GetStringAsync($"/Precios/{id}");

        Assert.Contains("Precio actual esperado", html);
        Assert.Contains("117.00", html);
        Assert.Contains("advertencia", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/Precios/{id}/Publicar", html);
    }
```

- [ ] **Step 3: Ejecutar y confirmar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~DetallesAdvierteDiferenciasDePrecioActualSinBloquearPublicar`
Expected: FAIL (la vista actual no tiene la columna «Precio actual esperado» ni la clase `advertencia`)

- [ ] **Step 4: Actualizar la vista**

En `Detalles.cshtml`, cambia el bloque `@{ ... }` inicial de:

```cshtml
@model VistaDetalles
@{
    var notificacion = Model.Notificacion;
    ViewData["Titulo"] = "Notificación de precios";
    ViewData["Seccion"] = "precios";
}
```

a:

```cshtml
@model VistaDetalles
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

Y cambia la sección «Detalles de precio» de:

```cshtml
<section class="tarjeta tarjeta--tabla">
    <h2>Detalles de precio</h2>
    @if (notificacion.Detalles.Count == 0)
    {
        <p class="vacio__texto">El borrador aún no tiene detalles de precio.</p>
    }
    else
    {
        <table class="tabla">
            <thead>
                <tr>
                    <th scope="col">Tipo de alimento</th>
                    <th scope="col">Presentación</th>
                    <th scope="col">Precio final por 40 kg</th>
                    <th scope="col">Precio actual del documento</th>
                    <th scope="col">Edades recomendadas</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var detalle in notificacion.Detalles)
                {
                    <tr>
                        <td>@detalle.TipoAlimento</td>
                        <td>@detalle.Presentacion</td>
                        <td>@detalle.PrecioFinalPor40Kg.ToString("0.00")</td>
                        <td>@(detalle.PrecioActualDocumento?.ToString("0.00") ?? "—")</td>
                        <td>@(FechasEdad(detalle.EdadDesdeDias, detalle.EdadHastaDias))</td>
                    </tr>
                }
            </tbody>
        </table>
    }
</section>
```

a:

```cshtml
<section class="tarjeta tarjeta--tabla">
    <h2>Detalles de precio</h2>
    @if (hayAdvertencias)
    {
        <div class="alerta alerta--aviso" role="note">
            <strong>Advertencia:</strong> el «PRECIO ACTUAL» de una o más líneas no
            coincide con el precio vigente esperado a la fecha del documento. Es
            informativo: no impide publicar; revise el borrador si corresponde.
        </div>
    }
    @if (notificacion.Detalles.Count == 0)
    {
        <p class="vacio__texto">El borrador aún no tiene detalles de precio.</p>
    }
    else
    {
        <table class="tabla">
            <thead>
                <tr>
                    <th scope="col">Tipo de alimento</th>
                    <th scope="col">Presentación</th>
                    <th scope="col">Precio final por 40 kg</th>
                    <th scope="col">Precio actual del documento</th>
                    <th scope="col">Precio actual esperado</th>
                    <th scope="col">Edades recomendadas</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var detalle in notificacion.Detalles)
                {
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
                }
            </tbody>
        </table>
    }
</section>
```

El resto del archivo (cabecera, resumen, acciones, `@functions { FechasEdad }`) queda igual.

- [ ] **Step 5: Ejecutar el test nuevo y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~DetallesAdvierteDiferenciasDePrecioActualSinBloquearPublicar`
Expected: PASS

- [ ] **Step 6: Ejecutar toda la suite de `Trajano.GestorCaisy.Tests` para descartar roturas colaterales (otros tests usan el mismo fixture `CrearDetalle`)**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/Detalles.cshtml Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs
git commit -m "feat(gestor-caisy): advertencia visual no bloqueante de precio actual en precios de alimento"
```

---

### Task 8: Actualizar el glosario de dominio

**Files:**
- Modify: `docs/dominio/glosario-avicola.md`

**Interfaces:** ninguna; cambio solo documental.

- [ ] **Step 1: Corregir la fila de huevo que hoy contrasta con alimento**

En la sección «Precios de huevo (SP9A)», cambia:

```markdown
| Precio actual del documento | Columna «Precio Actual» del Excel de CAISY: control informativo contra la publicación vigente. A diferencia de alimento, **no bloquea** la publicación. |
```

por:

```markdown
| Precio actual del documento | Columna «Precio Actual» del Excel de CAISY: control informativo contra la publicación vigente. No bloquea la publicación. |
```

- [ ] **Step 2: Agregar la fila equivalente en «Pedidos de alimento (SP8)»**

En esa sección, después de la fila «Precio final por 40 kg», agrega:

```markdown
| Precio actual del documento (alimento) | Columna «Precio Actual» del PDF o Excel importado: control informativo contra la publicación vigente a la fecha del documento, para el mismo tipo y presentación. No bloquea la publicación (spec 2026-09-15, igual que huevo). |
```

- [ ] **Step 3: Ejecutar el gate mínimo para cambio documental**

Run: `./verify.ps1 -SoloDocumentacion` (o el flag equivalente que `docs/ai/PUERTA_CALIDAD.md` indique para mojibake, enlaces y `git diff --check`; si no existe ese flag, ejecutar los tres chequeos sueltos que ese documento describe).
Expected: PASS, sin mojibake ni problemas de espacios en blanco.

- [ ] **Step 4: Commit**

```bash
git add docs/dominio/glosario-avicola.md
git commit -m "docs(glosario): alinea precio actual del documento entre alimento y huevo"
```

---

### Task 9: Puerta de calidad completa y push

- [ ] **Step 1: Ejecutar la puerta de calidad completa (requiere Docker corriendo por los tests de integración)**

Run: `./verify.ps1`
Expected: PASS. Si falla, arreglar el contenido señalado — nunca relajar un gate ni usar `--no-verify`.

- [ ] **Step 2: Revisar el diff completo antes de empujar**

Run: `git log --oneline -9` y `git diff develop@{upstream}..HEAD --stat` (o el equivalente si la rama no tiene upstream todavía)
Expected: solo los 9 commits de este plan, sin archivos inesperados.

- [ ] **Step 3: Pedir confirmación explícita al usuario antes de ejecutar `git push`**

Este repositorio trabaja sin pull requests: `develop` recibe commits y push directos, pero cada push se confirma con el usuario antes de ejecutarse (no hay autorización blanket en este plan). Una vez confirmado:

```bash
git push
```
