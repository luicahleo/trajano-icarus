# SP7 — Vacunación Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir al módulo `GestionAvicola` la vacunación: catálogo global de programas (lo sube el Administrador vía formulario + Excel), asignación de planes a galpones por parte del cliente (tareas materializadas con snapshot, `FechaProgramada = FechaNacimientoLote + EdadDia`), notificación de tareas pendientes (vencidas/hoy + próximas 7 días), completar (cliente o trabajador con `Vacunacion`) y cancelar (solo cliente), con historial sanitario preservado (nada se borra físicamente).

**Architecture:** Mismo bounded context y schema (`gestion_avicola`). Dos agregados nuevos: `ProgramaVacunacion` (raíz, catálogo global **sin tenant**, con hijas `ItemPlanVacunacion`) y `TareaVacunacion` (raíz propia, del tenant, `ClienteId` desnormalizado, snapshot del ítem, estados Pendiente/Completada/Cancelada). Application con commands/queries/handlers/validators compactos en `Vacunacion/`, patrón SP5/SP6. La importación Excel (ClosedXML) vive en Infrastructure detrás de `IImportadorCronogramaVacunacion` (Application no se acopla a la librería). Endpoints en `GestionAvicolaEndpoints.cs` con las políticas existentes más dos nuevas (`CatalogoVacunacion` = `Funcionalidad:Vacunacion` OR rol Administrador; `SoloCliente` = claim rol Cliente). Frontend: TanStack Query + MUI, online-first, secciones bajo `RequiereFuncionalidad`/`useFuncionalidad`.

**Tech Stack:** .NET 10, EF Core 10 (SqlServer), MediatR, FluentValidation, ClosedXML 0.105.0 (la del legacy), xUnit + NSubstitute, Testcontainers.MsSql (Docker corriendo), React 19 + MUI + TanStack Query + vitest.

**Spec:** `docs/superpowers/specs/2026-08-19-sp7-vacunacion-design.md` (leerlo primero; es la fuente de las reglas).

## Global Constraints

- Idioma: identificadores, mensajes y tests en español correcto con acentos; UTF-8 sin BOM; nunca mojibake. Mensajes de commit en el estilo del repo (minúsculas, sin acentos: `feat(avicola): ...`).
- Anti-PII: errores genéricos; nunca nombres de vacuna, motivos ni observaciones en el registro de vuelo (texto libre: podría contener PII). `CompletadaPor` guarda el **id** del usuario, nunca el nombre. Anti-enumeración: id de otro tenant = 404 (`NotFoundException` genérico).
- TDD: cada test se ve en rojo antes de implementar (para tipos nuevos, el rojo es el error de compilación). Tests en español estilo frase.
- `TreatWarningsAsErrors=true` con Roslynator y SonarAnalyzer: build sin warnings.
- `sealed` en todo; `sealed record` para commands/queries/DTOs; `sealed class` para entidades, handlers, validators y repositorios.
- `IUnitOfWork` genérica NO se usa en este módulo: siempre `IUnidadTrabajoGestionAvicola`.
- Filtros globales EF **sin `.Value`** sobre el `Guid?` del tenant. `ProgramaVacunacion` e `ItemPlanVacunacion` llevan filtro solo de `EstaActivo` (catálogo global, sin tenant); `TareaVacunacion` lleva `EstaActivo && tenant`.
- Fechas de negocio con `DateOnly` y `DateTime.UtcNow`. Nada de `DateTime.Now`.
- La `FechaAplicacion` la informa el usuario (por defecto hoy en el handler, nunca futura: lo valida el dominio). A diferencia de SP6, el servidor NO fija la fecha.
- Soft delete en todo: `DELETE` = `Desactivar()`, nunca borrado físico. Al asignar un plan nuevo se desactivan solo las tareas **pendientes** del plan anterior; completadas y canceladas quedan como historial sanitario.
- La columna FECHA del Excel se ignora: la fuente de verdad es EDAD (días).
- Rutas relativas a la raíz del repo (`Trajano-Icarus/`). Docker corriendo para los tests de integración.
- Commits por tarea con el test dirigido en verde; puerta completa (`./verify.ps1`) antes del push final. Prohibido `--no-verify`; si un gate falla, se arregla el contenido.
- La política `Funcionalidad:Vacunacion` ya se autorregistra (bucle sobre el enum en `AddClientesInfraestructura`): no hay que registrarla a mano.
- Los ensamblados de MediatR/FluentValidation de GestionAvicola ya están registrados en `Program.cs`: los handlers y validators nuevos se recogen solos; `Program.cs` NO se modifica.

## File Structure

Backend — crear:

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ProgramaVacunacion.cs` — agregado raíz del catálogo global.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ItemPlanVacunacion.cs` — entidad hija (ítem por edad en días) + record `DatosItemPlanVacunacion`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TareaVacunacion.cs` — agregado raíz del tenant con snapshot y estados.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoTareaVacunacion.cs` — enum Pendiente/Completada/Cancelada.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioProgramasVacunacion.cs` — interfaz + DTOs de programas.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioTareasVacunacion.cs` — interfaz + DTOs de tareas.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IImportadorCronogramaVacunacion.cs` — interfaz del parseo Excel + records de resultado.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/Programas.cs` — commands/queries/handlers/validators del catálogo (crear, actualizar, importar cronograma, desactivar, listar, obtener).
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/AsignacionPlan.cs` — asignar/quitar plan a galpón.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/EjecucionTareas.cs` — completar/cancelar tarea.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/ConsultasTareas.cs` — historial por galpón y notificación del tenant.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionProgramaVacunacion.cs`, `ConfiguracionItemPlanVacunacion.cs`, `ConfiguracionTareaVacunacion.cs` — mapeos EF (longitudes, índices, checks).
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioProgramasVacunacion.cs`, `RepositorioTareasVacunacion.cs`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorCronogramaVacunacion.cs` — parseo ClosedXML tolerante.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/` — migración `Vacunacion` generada por `dotnet ef`.
- `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoCatalogoVacunacion.cs` — requirement + handler de la política `CatalogoVacunacion`.
- Tests unitarios: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramaVacunacionTests.cs`, `TareaVacunacionTests.cs`, `ProgramasVacunacionHandlerTests.cs`, `AsignacionPlanVacunacionHandlerTests.cs`, `EjecucionTareasVacunacionHandlerTests.cs`, `ConsultasTareasVacunacionHandlerTests.cs`, `ImportadorCronogramaVacunacionTests.cs`; `Icarus/tests/Icarus.UnitTests/Clientes/ManejadorCatalogoVacunacionTests.cs`.
- Tests de integración: `Icarus/tests/Icarus.IntegrationTests/VacunacionEndpointsTests.cs`.

Backend — modificar:

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs` — DbSets y filtros.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs` — repos + importador.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj` — `PackageReference ClosedXML`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaGestionAvicola.cs` — programa demo con cronograma.
- `Icarus/Directory.Packages.props` — `PackageVersion ClosedXML 0.105.0`.
- `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs` — `Vacunacion` pasa a ser asignable.
- `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs` — mueve `Vacunacion` a la teoría de asignables.
- `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs` — OR de `GestionAvicolaEstructura` con `Vacunacion` + política `CatalogoVacunacion`.
- `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/PoliticasClientes.cs` — constante `CatalogoVacunacion`.
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs` y `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs` — política `SoloCliente`.
- `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs` — endpoints de vacunación.

Frontend — crear:

- `web/src/features/admin/vacunacion/AdminVacunacionPage.tsx` (+ `.test.tsx`) — lista, alta/edición, subida de Excel con errores por fila, desactivar.
- `web/src/features/avicola/VacunacionNotificacion.tsx` (+ `.test.tsx`) — VencidasYHoy + Próximas con completar/cancelar.
- `web/src/features/avicola/CompletarTareaDialog.tsx` (+ `.test.tsx`), `CancelarTareaDialog.tsx`, `AsignarPlanDialog.tsx` (+ `.test.tsx`).

Frontend — modificar:

- `web/src/lib/tipos.ts` — `'Vacunacion'` en `FuncionalidadOperativaTrabajador` + tipos nuevos.
- `web/src/lib/http.ts` — `peticion` acepta `FormData` (subida del Excel).
- `web/src/features/avicola/api.ts` (+ `api.test.ts`) — funciones de vacunación.
- `web/src/features/avicola/constantes.ts` — claves de query.
- `web/src/features/trabajadores/TrabajadoresPage.tsx` (+ `.test.tsx`) — checkbox Vacunación.
- `web/src/app/AppLayout.tsx` — enlace admin Vacunación + condición del enlace avícola.
- `web/src/app/router.tsx` — `'Vacunacion'` en las guardas de `/avicola*` + ruta `/admin/vacunacion`.
- `web/src/app/paginasDiferidas.tsx` — lazy de `AdminVacunacionPage`.
- `web/src/features/avicola/GalponesPage.tsx` — monta `VacunacionNotificacion`.
- `web/src/features/avicola/GalponPage.tsx` — sección Vacunación (historial + asignar/quitar plan).

Documentación — modificar al cerrar: `AGENTS.md` (sección Proyecto) + adaptadores regenerados.

---

### Task 1: Dominio `ProgramaVacunacion` + `ItemPlanVacunacion` (TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramaVacunacionTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ProgramaVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ItemPlanVacunacion.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `Entity`, `ReglaNegocioException` (`Icarus.BuildingBlocks.Domain`).
- Produces: `ProgramaVacunacion(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)` (+ sobrecarga con `Guid id` primero para semilla/tests); propiedades `Nombre`, `FechaEmision`, `CantidadAves`, `Observaciones`, `EstaActivo`, `Items` (`IReadOnlyCollection<ItemPlanVacunacion>`); métodos `ActualizarDatos(string, DateOnly, int, string?)`, `ReemplazarCronograma(IEnumerable<DatosItemPlanVacunacion>)`, `Desactivar()`. `DatosItemPlanVacunacion(int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones)`. `ItemPlanVacunacion`: propiedades `EdadDia`, `Vacuna`, `ModoAplicacion`, `Observaciones`, `EstaActivo`. Los usan las Tasks 3, 4, 7 y 9.

- [ ] **Step 1: Escribir el test que falla**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramaVacunacionTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ProgramaVacunacionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private static ProgramaVacunacion CrearPrograma() =>
        new("PROGRAMA DE VACUNACION PARA 1000 AVES", Hoy.AddDays(-30), 1000, null);

    [Fact]
    public void CtorValidoAsignaYNaceActivo()
    {
        var programa = CrearPrograma();
        Assert.Equal("PROGRAMA DE VACUNACION PARA 1000 AVES", programa.Nombre);
        Assert.Equal(Hoy.AddDays(-30), programa.FechaEmision);
        Assert.Equal(1000, programa.CantidadAves);
        Assert.True(programa.EstaActivo);
        Assert.Empty(programa.Items);
    }

    [Fact]
    public void CtorNombreVacioLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("  ", Hoy, 1000, null));
        Assert.Equal("El nombre del programa es obligatorio.", ex.Message);
    }

    [Fact]
    public void CtorFechaEmisionFuturaLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("Plan", Hoy.AddDays(1), 1000, null));
        Assert.Equal("La fecha de emisión no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CtorCantidadAvesInvalidaLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("Plan", Hoy, 0, null));
        Assert.Equal("La cantidad de aves debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void ReemplazarCronogramaCreaItemsActivos()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
            new DatosItemPlanVacunacion(10, "NEWCASTLE + BRONQUITIS", "Gota ocular", "Ayuno de agua 2 horas"),
        ]);
        Assert.Equal(2, programa.Items.Count);
        Assert.All(programa.Items, i => Assert.True(i.EstaActivo));
        var primero = programa.Items.Single(i => i.EdadDia == 3);
        Assert.Equal("BIO COCCIVET R", primero.Vacuna);
        Assert.Equal("Agua de bebida", primero.ModoAplicacion);
        Assert.NotEqual(Guid.Empty, primero.Id);
    }

    [Fact]
    public void ReemplazarCronogramaDesactivaLosAnterioresSinBorrarlos()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, "BIO COCCIVET R", null, null)]);
        var anterior = programa.Items.Single();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(7, "GUMBORO", null, null)]);
        Assert.False(anterior.EstaActivo);
        Assert.Equal(2, programa.Items.Count);
        Assert.Single(programa.Items.Where(i => i.EstaActivo));
    }

    [Fact]
    public void ReemplazarCronogramaConEdadDuplicadaLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() => programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "A", null, null),
            new DatosItemPlanVacunacion(3, "B", null, null)]));
        Assert.Equal("El cronograma no puede repetir la edad en días entre ítems.", ex.Message);
    }

    [Fact]
    public void ReemplazarCronogramaVacioLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() => programa.ReemplazarCronograma([]));
        Assert.Equal("El cronograma debe tener al menos un ítem.", ex.Message);
    }

    [Fact]
    public void ItemSinEdadLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            programa.ReemplazarCronograma([new DatosItemPlanVacunacion(0, "A", null, null)]));
        Assert.Equal("La edad en días debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void ItemSinVacunaLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, " ", null, null)]));
        Assert.Equal("La vacuna del ítem es obligatoria.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosModificaLosDatosBasicos()
    {
        var programa = CrearPrograma();
        programa.ActualizarDatos("PLAN NUEVO", Hoy.AddDays(-5), 2000, "  Observación  ");
        Assert.Equal("PLAN NUEVO", programa.Nombre);
        Assert.Equal(Hoy.AddDays(-5), programa.FechaEmision);
        Assert.Equal(2000, programa.CantidadAves);
        Assert.Equal("Observación", programa.Observaciones);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var programa = CrearPrograma();
        programa.Desactivar();
        Assert.False(programa.EstaActivo);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ProgramaVacunacionTests"`
Expected: FALLA la compilación (los tipos no existen).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ProgramaVacunacion.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Catálogo global de planes de vacunación (spec SP7): lo emite CAISY y hoy lo
// sube el Administrador; no lleva ClienteId. El papel agrupa varias vacunas
// del mismo día en una fila: la EdadDia no se repite entre ítems activos. El
// cronograma se reemplaza en bloque; las tareas ya materializadas en galpones
// tienen snapshot y no se tocan. Un programa desactivado no es asignable.
public sealed class ProgramaVacunacion : AggregateRoot
{
    private readonly List<ItemPlanVacunacion> _items = [];

    private ProgramaVacunacion()
    {
    }

    public ProgramaVacunacion(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
    {
        AsignarDatos(nombre, fechaEmision, cantidadAves, observaciones);
        EstaActivo = true;
    }

    // Para la semilla y tests que necesitan ids fijos.
    public ProgramaVacunacion(Guid id, string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
        : this(nombre, fechaEmision, cantidadAves, observaciones) => Id = id;

    public string Nombre { get; private set; } = string.Empty;

    public DateOnly FechaEmision { get; private set; }

    public int CantidadAves { get; private set; }

    public string? Observaciones { get; private set; }

    public bool EstaActivo { get; private set; }

    public IReadOnlyCollection<ItemPlanVacunacion> Items => _items.AsReadOnly();

    public void ActualizarDatos(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones) =>
        AsignarDatos(nombre, fechaEmision, cantidadAves, observaciones);

    public void ReemplazarCronograma(IEnumerable<DatosItemPlanVacunacion> items)
    {
        var lista = items.ToList();
        if (lista.Count == 0)
            throw new ReglaNegocioException("El cronograma debe tener al menos un ítem.");
        if (lista.Select(i => i.EdadDia).Distinct().Count() != lista.Count)
            throw new ReglaNegocioException("El cronograma no puede repetir la edad en días entre ítems.");

        foreach (var item in _items.Where(i => i.EstaActivo))
            item.Desactivar();
        foreach (var datos in lista)
            _items.Add(new ItemPlanVacunacion(datos.EdadDia, datos.Vacuna, datos.ModoAplicacion, datos.Observaciones));
    }

    public void Desactivar() => EstaActivo = false;

    private void AsignarDatos(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre del programa es obligatorio.");
        if (fechaEmision > Hoy())
            throw new ReglaNegocioException("La fecha de emisión no puede ser futura.");
        if (cantidadAves <= 0)
            throw new ReglaNegocioException("La cantidad de aves debe ser mayor que cero.");

        Nombre = nombre.Trim();
        FechaEmision = fechaEmision;
        CantidadAves = cantidadAves;
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ItemPlanVacunacion.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Datos de entrada de un ítem del cronograma (spec SP7).
public sealed record DatosItemPlanVacunacion(int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones);

// Ítem del cronograma (spec SP7): "a los N días de edad del lote, aplicar X".
// Vacuna es texto libre: también cubre los manejos del papel de CAISY
// (desparasitación, recorte de pico, traslado). Hija del agregado
// ProgramaVacunacion: solo se crea y desactiva a través de la raíz.
public sealed class ItemPlanVacunacion : Entity
{
    private ItemPlanVacunacion()
    {
    }

    internal ItemPlanVacunacion(int edadDia, string vacuna, string? modoAplicacion, string? observaciones)
    {
        if (edadDia <= 0)
            throw new ReglaNegocioException("La edad en días debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(vacuna))
            throw new ReglaNegocioException("La vacuna del ítem es obligatoria.");

        Id = Guid.NewGuid();
        EdadDia = edadDia;
        Vacuna = vacuna.Trim();
        ModoAplicacion = string.IsNullOrWhiteSpace(modoAplicacion) ? null : modoAplicacion.Trim();
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        EstaActivo = true;
    }

    public int EdadDia { get; private set; }

    public string Vacuna { get; private set; } = string.Empty;

    public string? ModoAplicacion { get; private set; }

    public string? Observaciones { get; private set; }

    public bool EstaActivo { get; private set; }

    internal void Desactivar() => EstaActivo = false;
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ProgramaVacunacionTests"`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ProgramaVacunacion.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ItemPlanVacunacion.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramaVacunacionTests.cs
git commit -m "feat(avicola): agregado ProgramaVacunacion con items por edad"
```

---

### Task 2: Dominio `TareaVacunacion` + `EstadoTareaVacunacion` (TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/TareaVacunacionTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoTareaVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TareaVacunacion.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `ReglaNegocioException`.
- Produces: `EstadoTareaVacunacion` (`Pendiente`, `Completada`, `Cancelada`); `TareaVacunacion(Guid galponId, Guid clienteId, Guid programaVacunacionId, Guid itemPlanVacunacionId, int edadDia, string vacuna, string? modoAplicacion, string? observacionesProgramadas, DateOnly fechaProgramada)` (+ sobrecarga con `Guid id` primero); propiedades `GalponId`, `ClienteId`, `ProgramaVacunacionId`, `ItemPlanVacunacionId`, `EdadDia`, `Vacuna`, `ModoAplicacion`, `ObservacionesProgramadas`, `FechaProgramada`, `Estado`, `FechaAplicacion`, `AvesVacunadas`, `CompletadaPor`, `ObservacionesAplicacion`, `MotivoCancelacion`, `EstaActivo`; métodos `Completar(DateOnly fechaAplicacion, int? avesVacunadas, Guid completadaPor, string? observaciones)`, `Cancelar(string? motivo)`, `Desactivar()`. Los usan las Tasks 4, 5, 6 y 7.

- [ ] **Step 1: Escribir el test que falla**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/TareaVacunacionTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class TareaVacunacionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private static TareaVacunacion TareaPendiente(DateOnly? fechaProgramada = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            3, "BIO COCCIVET R", "Agua de bebida", null, fechaProgramada ?? Hoy);

    [Fact]
    public void CtorValidoNacePendienteConSnapshot()
    {
        var tarea = TareaPendiente();
        Assert.Equal(EstadoTareaVacunacion.Pendiente, tarea.Estado);
        Assert.Equal(3, tarea.EdadDia);
        Assert.Equal("BIO COCCIVET R", tarea.Vacuna);
        Assert.True(tarea.EstaActivo);
        Assert.Null(tarea.FechaAplicacion);
        Assert.Null(tarea.AvesVacunadas);
        Assert.Null(tarea.CompletadaPor);
    }

    [Fact]
    public void CtorSinVacunaLanzaReglaNegocio() =>
        Assert.Throws<ReglaNegocioException>(() =>
            new TareaVacunacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                3, " ", null, null, Hoy));

    [Fact]
    public void CompletarConFechaFuturaLanzaReglaNegocio()
    {
        var tarea = TareaPendiente();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy.AddDays(1), null, Guid.NewGuid(), null));
        Assert.Equal("La fecha de aplicación no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CompletarConAvesCeroLanzaReglaNegocio()
    {
        var tarea = TareaPendiente();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy, 0, Guid.NewGuid(), null));
        Assert.Equal("Las aves vacunadas deben ser mayores que cero.", ex.Message);
    }

    [Fact]
    public void CompletarRegistraFechaAvesYUsuario()
    {
        var tarea = TareaPendiente();
        var usuario = Guid.NewGuid();
        tarea.Completar(Hoy.AddDays(-1), 950, usuario, "Aplicación parcial por faltante de agua.");
        Assert.Equal(EstadoTareaVacunacion.Completada, tarea.Estado);
        Assert.Equal(Hoy.AddDays(-1), tarea.FechaAplicacion);
        Assert.Equal(950, tarea.AvesVacunadas);
        Assert.Equal(usuario, tarea.CompletadaPor);
        Assert.Equal("Aplicación parcial por faltante de agua.", tarea.ObservacionesAplicacion);
    }

    [Fact]
    public void CompletarDosVecesLanzaSelladoPorEstado()
    {
        var tarea = TareaPendiente();
        tarea.Completar(Hoy, null, Guid.NewGuid(), null);
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy, null, Guid.NewGuid(), null));
        Assert.Equal("La tarea ya está cerrada.", ex.Message);
    }

    [Fact]
    public void CancelarRegistraMotivoYSella()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar("Lote con mortalidad alta, se posterga.");
        Assert.Equal(EstadoTareaVacunacion.Cancelada, tarea.Estado);
        Assert.Equal("Lote con mortalidad alta, se posterga.", tarea.MotivoCancelacion);
        Assert.Throws<ReglaNegocioException>(() => tarea.Cancelar(null));
        Assert.Throws<ReglaNegocioException>(() => tarea.Completar(Hoy, null, Guid.NewGuid(), null));
    }

    [Fact]
    public void CancelarSinMotivoQuedaSinMotivo()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar(null);
        Assert.Null(tarea.MotivoCancelacion);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var tarea = TareaPendiente();
        tarea.Desactivar();
        Assert.False(tarea.EstaActivo);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~TareaVacunacionTests"`
Expected: FALLA la compilación (los tipos no existen).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoTareaVacunacion.cs`:

```csharp
namespace Icarus.GestionAvicola.Domain;

// Destinos de una tarea de vacunación (spec SP7): nace Pendiente y se cierra
// Completada o Cancelada; sin reprogramación individual (si el plan cambia,
// se corrige el plan o se reasigna el galpón).
public enum EstadoTareaVacunacion
{
    Pendiente,
    Completada,
    Cancelada,
}
```

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TareaVacunacion.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Tarea materializada al asignar un plan a un galpón (spec SP7): copia el
// snapshot del ítem (el catálogo puede cambiar y el historial sanitario no).
// ClienteId va desnormalizado para el filtro de tenant sin join, patrón de
// SP5/SP6. Completar registra lo que pasó (fecha informada por el usuario,
// nunca futura); cancelar es decisión del cliente. CompletadaPor guarda el id
// del usuario, no el nombre (anti-PII).
public sealed class TareaVacunacion : AggregateRoot
{
    private TareaVacunacion()
    {
    }

    public TareaVacunacion(
        Guid galponId, Guid clienteId, Guid programaVacunacionId, Guid itemPlanVacunacionId,
        int edadDia, string vacuna, string? modoAplicacion, string? observacionesProgramadas,
        DateOnly fechaProgramada)
    {
        if (galponId == Guid.Empty)
            throw new ReglaNegocioException("La tarea debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La tarea debe pertenecer a un cliente.");
        if (edadDia <= 0)
            throw new ReglaNegocioException("La edad en días debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(vacuna))
            throw new ReglaNegocioException("La vacuna de la tarea es obligatoria.");

        GalponId = galponId;
        ClienteId = clienteId;
        ProgramaVacunacionId = programaVacunacionId;
        ItemPlanVacunacionId = itemPlanVacunacionId;
        EdadDia = edadDia;
        Vacuna = vacuna.Trim();
        ModoAplicacion = string.IsNullOrWhiteSpace(modoAplicacion) ? null : modoAplicacion.Trim();
        ObservacionesProgramadas = string.IsNullOrWhiteSpace(observacionesProgramadas) ? null : observacionesProgramadas.Trim();
        FechaProgramada = fechaProgramada;
        Estado = EstadoTareaVacunacion.Pendiente;
        EstaActivo = true;
    }

    // Para tests que necesitan ids fijos.
    public TareaVacunacion(
        Guid id, Guid galponId, Guid clienteId, Guid programaVacunacionId, Guid itemPlanVacunacionId,
        int edadDia, string vacuna, string? modoAplicacion, string? observacionesProgramadas,
        DateOnly fechaProgramada)
        : this(galponId, clienteId, programaVacunacionId, itemPlanVacunacionId,
            edadDia, vacuna, modoAplicacion, observacionesProgramadas, fechaProgramada) => Id = id;

    public Guid GalponId { get; private set; }

    public Guid ClienteId { get; private set; }

    public Guid ProgramaVacunacionId { get; private set; }

    public Guid ItemPlanVacunacionId { get; private set; }

    public int EdadDia { get; private set; }

    public string Vacuna { get; private set; } = string.Empty;

    public string? ModoAplicacion { get; private set; }

    public string? ObservacionesProgramadas { get; private set; }

    public DateOnly FechaProgramada { get; private set; }

    public EstadoTareaVacunacion Estado { get; private set; }

    public DateOnly? FechaAplicacion { get; private set; }

    public int? AvesVacunadas { get; private set; }

    public Guid? CompletadaPor { get; private set; }

    public string? ObservacionesAplicacion { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public bool EstaActivo { get; private set; }

    public void Completar(DateOnly fechaAplicacion, int? avesVacunadas, Guid completadaPor, string? observaciones)
    {
        ExigirPendiente();
        if (fechaAplicacion > Hoy())
            throw new ReglaNegocioException("La fecha de aplicación no puede ser futura.");
        if (avesVacunadas is <= 0)
            throw new ReglaNegocioException("Las aves vacunadas deben ser mayores que cero.");
        if (completadaPor == Guid.Empty)
            throw new ReglaNegocioException("La aplicación debe registrar el usuario que la informó.");

        FechaAplicacion = fechaAplicacion;
        AvesVacunadas = avesVacunadas;
        CompletadaPor = completadaPor;
        ObservacionesAplicacion = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        Estado = EstadoTareaVacunacion.Completada;
    }

    public void Cancelar(string? motivo)
    {
        ExigirPendiente();
        MotivoCancelacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        Estado = EstadoTareaVacunacion.Cancelada;
    }

    // Soft delete (glosario): al reasignar o quitar el plan se desactivan las
    // pendientes; las completadas y canceladas quedan como historial sanitario.
    public void Desactivar() => EstaActivo = false;

    private void ExigirPendiente()
    {
        if (Estado != EstadoTareaVacunacion.Pendiente)
            throw new ReglaNegocioException("La tarea ya está cerrada.");
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~TareaVacunacionTests"`
Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TareaVacunacion.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoTareaVacunacion.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/TareaVacunacionTests.cs
git commit -m "feat(avicola): agregado TareaVacunacion con snapshot y sellado por estado"
```

---

### Task 3: Application del catálogo de programas (CRUD + importación Excel, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioProgramasVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IImportadorCronogramaVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/Programas.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramasVacunacionHandlerTests.cs`

**Interfaces:**
- Consumes: `ProgramaVacunacion`, `DatosItemPlanVacunacion` (Task 1); `IUnidadTrabajoGestionAvicola`, `ICurrentUser`, `IRegistroVuelo`, `IOperacionRegistrable`, `DescriptorOperacionRegistroVuelo`, `DatoRegistroVuelo`, `NotFoundException`, `ConflictException`, `FluentValidation`.
- Produces:
  - `IRepositorioProgramasVacunacion`: `void Agregar(ProgramaVacunacion programa)`; `Task<ProgramaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)` (filtro `EstaActivo`; para asignación y lectura operativa); `Task<ProgramaVacunacion?> ObtenerPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default)` (rol de plataforma); `Task<bool> ExisteNombreAsync(string nombre, Guid? excluyendoId = null, CancellationToken ct = default)` (incluyendo inactivos: el soft delete no libera el nombre); `Task<IReadOnlyList<ProgramaVacunacion>> ListarAsync(bool incluirInactivos, CancellationToken ct = default)`.
  - `IImportadorCronogramaVacunacion.Importar(Stream contenido) → ResultadoImportacionCronograma`; records `ItemCronogramaImportado(int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones)`, `ErrorFilaImportacion(int Fila, string Mensaje)`, `ResultadoImportacionCronograma(IReadOnlyList<ItemCronogramaImportado> Items, IReadOnlyList<ErrorFilaImportacion> Errores)`.
  - Commands: `CrearProgramaVacunacionCommand(string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones) : IRequest<Guid>`; `ActualizarProgramaVacunacionCommand(Guid ProgramaId, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones) : IRequest`; `ImportarCronogramaExcelCommand(Guid ProgramaId, Stream Contenido) : IRequest<int>` (devuelve ítems importados); `DesactivarProgramaVacunacionCommand(Guid ProgramaId) : IRequest`; queries `ListarProgramasVacunacionQuery(bool IncluirInactivos) : IRequest<IReadOnlyList<ProgramaVacunacionResumen>>` y `ObtenerProgramaVacunacionQuery(Guid ProgramaId) : IRequest<ProgramaVacunacionDetalle>`.
  - DTOs: `ProgramaVacunacionResumen(Guid Id, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones, bool EstaActivo)`, `ItemPlanVacunacionResumen(Guid Id, int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones)`, `ProgramaVacunacionDetalle(Guid Id, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones, bool EstaActivo, IReadOnlyList<ItemPlanVacunacionResumen> Items)`.

- [ ] **Step 1: Escribir los tests que fallan**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramasVacunacionHandlerTests.cs`:

```csharp
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ProgramasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioProgramasVacunacion _programas = Substitute.For<IRepositorioProgramasVacunacion>();
    private readonly IImportadorCronogramaVacunacion _importador = Substitute.For<IImportadorCronogramaVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static ProgramaVacunacion ProgramaDemo()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", Hoy.AddDays(-10), 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(10, "B", null, null),
            new DatosItemPlanVacunacion(3, "A", "Agua de bebida", null),
        ]);
        return programa;
    }

    [Fact]
    public async Task CrearConDatosValidosGuardaYNarra()
    {
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var id = await handler.Handle(new("PLAN CAISY 1000", Hoy, 1000, null), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _programas.Received(1).Agregar(Arg.Is<ProgramaVacunacion>(p =>
            p.Nombre == "PLAN CAISY 1000" && p.CantidadAves == 1000 && p.EstaActivo));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir("avicola.vacunacion.programas.crear", "alta", "aplicada",
            Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    [Fact]
    public async Task CrearConNombreDuplicadoLanzaConflict()
    {
        _programas.ExisteNombreAsync("PLAN CAISY 1000", null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new("PLAN CAISY 1000", Hoy, 1000, null), CancellationToken.None));

        Assert.Equal("No se pudo registrar el programa de vacunación.", ex.Message);
        _programas.DidNotReceive().Agregar(Arg.Any<ProgramaVacunacion>());
    }

    [Fact]
    public async Task ActualizarInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), "X", Hoy, 100, null), CancellationToken.None));

        Assert.Equal("Programa de vacunación no encontrado.", ex.Message);
    }

    [Fact]
    public async Task ActualizarConNombreDeOtroProgramaLanzaConflict()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _programas.ExisteNombreAsync("OTRO PLAN", programa.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(programa.Id, "OTRO PLAN", Hoy, 100, null), CancellationToken.None));

        Assert.Equal("PLAN CAISY 1000", programa.Nombre);
    }

    [Fact]
    public async Task ActualizarConDatosValidosGuarda()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _programas.ExisteNombreAsync("PLAN RENOMBRADO", programa.Id, Arg.Any<CancellationToken>()).Returns(false);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id, "PLAN RENOMBRADO", Hoy, 2000, "nota"), CancellationToken.None);

        Assert.Equal("PLAN RENOMBRADO", programa.Nombre);
        Assert.Equal(2000, programa.CantidadAves);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConProgramaInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), new MemoryStream()), CancellationToken.None));
    }

    [Fact]
    public async Task ImportarConErroresLanzaValidationYSinGuardarNada()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [],
            [new ErrorFilaImportacion(3, "La edad debe ser un número entero mayor que cero.")]));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None));

        Assert.Equal("Fila 3: La edad debe ser un número entero mayor que cero.", ex.Errors.Single().ErrorMessage);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarValidoReemplazaElCronogramaYDevuelveLaCantidad()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [new ItemCronogramaImportado(1, "NEWCASTLE", "Gota ocular", null),
             new ItemCronogramaImportado(3, "BIO COCCIVET R", "Agua de bebida", "Ayuno 2 horas")],
            []));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var importados = await handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None);

        Assert.Equal(2, importados);
        Assert.Equal(4, programa.Items.Count);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        _vuelo.Received().Decidir("avicola.vacunacion.programas.importar-cronograma", "importacion", "aplicada",
            Arg.Any<IReadOnlyDictionary<string, object?>>());
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivarMarcaInactivoYGuarda()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _unidad);

        await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.False(programa.EstaActivo);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarSinRolAdministradorIgnoraElIncluirInactivos()
    {
        _usuario.Rol.Returns("Cliente");
        var handler = new ListarProgramasVacunacionHandler(_programas, _usuario);

        await handler.Handle(new(true), CancellationToken.None);

        await _programas.Received(1).ListarAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarComoAdministradorSiIncluyeInactivos()
    {
        _usuario.Rol.Returns("Administrador");
        _programas.ListarAsync(true, Arg.Any<CancellationToken>()).Returns([ProgramaDemo()]);
        var handler = new ListarProgramasVacunacionHandler(_programas, _usuario);

        var lista = await handler.Handle(new(true), CancellationToken.None);

        Assert.Single(lista);
        Assert.Equal("PLAN CAISY 1000", lista[0].Nombre);
    }

    [Fact]
    public async Task ObtenerInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerInactivoSinSerAdministradorLanzaNotFound()
    {
        var programa = ProgramaDemo();
        programa.Desactivar();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(programa.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerDevuelveDetalleConItemsActivosOrdenadosPorEdad()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        var detalle = await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.Equal(2, detalle.Items.Count);
        Assert.Equal(3, detalle.Items[0].EdadDia);
        Assert.Equal(10, detalle.Items[1].EdadDia);
        Assert.Equal("A", detalle.Items[0].Vacuna);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ProgramasVacunacionHandlerTests"`
Expected: FALLA la compilación (no existen los tipos de Application).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioProgramasVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Catálogo global (sin tenant, spec SP7): el filtro del contexto es solo
// EstaActivo. Los métodos "IncluyendoInactivos" ignoran ese filtro y son para
// el rol de plataforma (Administrador), que gestiona el catálogo; los
// operativos (ObtenerPorIdAsync, ListarAsync con incluirInactivos: false) son
// para cliente y trabajador.
public interface IRepositorioProgramasVacunacion
{
    void Agregar(ProgramaVacunacion programa);

    Task<ProgramaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProgramaVacunacion?> ObtenerPorIdIncluyendoInactivosAsync(Guid id, CancellationToken cancellationToken = default);

    // Unicidad incluyendo inactivos (spec SP7): el soft delete no libera el nombre.
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramaVacunacion>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
}

public sealed record ItemPlanVacunacionResumen(
    Guid Id, int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones);

public sealed record ProgramaVacunacionResumen(
    Guid Id, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones, bool EstaActivo);

public sealed record ProgramaVacunacionDetalle(
    Guid Id, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones,
    bool EstaActivo, IReadOnlyList<ItemPlanVacunacionResumen> Items);
```

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IImportadorCronogramaVacunacion.cs`:

```csharp
namespace Icarus.GestionAvicola.Application.Vacunacion;

// Parseo del Excel del plan (formato del papel de CAISY, spec SP7). La
// implementación vive en Infrastructure (ClosedXML); Application solo ve
// ítems o errores por número de fila. Todo-o-nada lo decide el handler: si
// hay errores no se guarda nada.
public interface IImportadorCronogramaVacunacion
{
    ResultadoImportacionCronograma Importar(Stream contenido);
}

public sealed record ItemCronogramaImportado(int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones);

public sealed record ErrorFilaImportacion(int Fila, string Mensaje);

public sealed record ResultadoImportacionCronograma(
    IReadOnlyList<ItemCronogramaImportado> Items, IReadOnlyList<ErrorFilaImportacion> Errores);
```

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/Programas.cs`:

```csharp
using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Registro de vuelo (spec SP7): solo campos no-PII (cantidades). Nunca
// nombres de vacuna, motivos ni observaciones (texto libre).
public sealed record CrearProgramaVacunacionCommand(string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.crear",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadAves"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarProgramaVacunacionCommand(Guid ProgramaId, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.actualizar",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadAves"] = DatoRegistroVuelo.Entero });
}

public sealed record ImportarCronogramaExcelCommand(Guid ProgramaId, Stream Contenido)
    : IRequest<int>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.importar-cronograma",
        new Dictionary<string, DatoRegistroVuelo> { ["ItemsImportados"] = DatoRegistroVuelo.Entero });
}

public sealed record DesactivarProgramaVacunacionCommand(Guid ProgramaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.desactivar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarProgramasVacunacionQuery(bool IncluirInactivos)
    : IRequest<IReadOnlyList<ProgramaVacunacionResumen>>;

public sealed record ObtenerProgramaVacunacionQuery(Guid ProgramaId) : IRequest<ProgramaVacunacionDetalle>;

public sealed class CrearProgramaVacunacionValidator : AbstractValidator<CrearProgramaVacunacionCommand>
{
    public CrearProgramaVacunacionValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CantidadAves).GreaterThan(0);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class ActualizarProgramaVacunacionValidator : AbstractValidator<ActualizarProgramaVacunacionCommand>
{
    public ActualizarProgramaVacunacionValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CantidadAves).GreaterThan(0);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class CrearProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CrearProgramaVacunacionCommand, Guid>
{
    public async Task<Guid> Handle(CrearProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        if (await programas.ExisteNombreAsync(request.Nombre.Trim(), null, cancellationToken))
            throw new ConflictException("No se pudo registrar el programa de vacunación.");
        var programa = new ProgramaVacunacion(request.Nombre, request.FechaEmision, request.CantidadAves, request.Observaciones);
        programas.Agregar(programa);
        registroVuelo.Decidir("avicola.vacunacion.programas.crear", "alta", "aplicada",
            new Dictionary<string, object?> { ["CantidadAves"] = programa.CantidadAves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return programa.Id;
    }
}

public sealed class ActualizarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ActualizarProgramaVacunacionCommand>
{
    public async Task Handle(ActualizarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        if (await programas.ExisteNombreAsync(request.Nombre.Trim(), programa.Id, cancellationToken))
            throw new ConflictException("No se pudo actualizar el programa de vacunación.");
        programa.ActualizarDatos(request.Nombre, request.FechaEmision, request.CantidadAves, request.Observaciones);
        registroVuelo.Decidir("avicola.vacunacion.programas.actualizar", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadAves"] = programa.CantidadAves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Todo-o-nada (spec SP7): una fila inválida rechaza la importación completa
// con la lista de errores por número de fila (ValidationException -> 400 con
// `errors`), sin guardar nada. La columna FECHA del Excel ya se ignoró en el
// importador: la fuente de verdad es EDAD.
public sealed class ImportarCronogramaExcelHandler(
    IRepositorioProgramasVacunacion programas, IImportadorCronogramaVacunacion importador,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarCronogramaExcelCommand, int>
{
    public async Task<int> Handle(ImportarCronogramaExcelCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var resultado = importador.Importar(request.Contenido);
        if (resultado.Errores.Count > 0)
            throw new ValidationException(resultado.Errores.Select(e =>
                new ValidationFailure("Cronograma", $"Fila {e.Fila}: {e.Mensaje}")));
        programa.ReemplazarCronograma(resultado.Items.Select(i =>
            new DatosItemPlanVacunacion(i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones)));
        registroVuelo.Decidir("avicola.vacunacion.programas.importar-cronograma", "importacion", "aplicada",
            new Dictionary<string, object?> { ["ItemsImportados"] = resultado.Items.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return resultado.Items.Count;
    }
}

public sealed class DesactivarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarProgramaVacunacionCommand>
{
    public async Task Handle(DesactivarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.Desactivar();
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// El catálogo es global (sin tenant). Los inactivos solo los ve el rol de
// plataforma: el nombre del rol es contrato del JWT (GestionAvicola no
// referencia Identity, regla de módulos).
public sealed class ListarProgramasVacunacionHandler(
    IRepositorioProgramasVacunacion programas, ICurrentUser usuario)
    : IRequestHandler<ListarProgramasVacunacionQuery, IReadOnlyList<ProgramaVacunacionResumen>>
{
    public async Task<IReadOnlyList<ProgramaVacunacionResumen>> Handle(
        ListarProgramasVacunacionQuery request, CancellationToken cancellationToken)
    {
        var incluirInactivos = request.IncluirInactivos
            && string.Equals(usuario.Rol, "Administrador", StringComparison.Ordinal);
        var programasLista = await programas.ListarAsync(incluirInactivos, cancellationToken);
        return programasLista.Select(p => new ProgramaVacunacionResumen(
            p.Id, p.Nombre, p.FechaEmision, p.CantidadAves, p.Observaciones, p.EstaActivo)).ToList();
    }
}

public sealed class ObtenerProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, ICurrentUser usuario)
    : IRequestHandler<ObtenerProgramaVacunacionQuery, ProgramaVacunacionDetalle>
{
    public async Task<ProgramaVacunacionDetalle> Handle(
        ObtenerProgramaVacunacionQuery request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var esAdministrador = string.Equals(usuario.Rol, "Administrador", StringComparison.Ordinal);
        if (!programa.EstaActivo && !esAdministrador)
            throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        return new ProgramaVacunacionDetalle(
            programa.Id, programa.Nombre, programa.FechaEmision, programa.CantidadAves,
            programa.Observaciones, programa.EstaActivo,
            programa.Items.Where(i => i.EstaActivo).OrderBy(i => i.EdadDia)
                .Select(i => new ItemPlanVacunacionResumen(i.Id, i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones))
                .ToList());
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ProgramasVacunacionHandlerTests"`
Expected: PASS (14 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioProgramasVacunacion.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IImportadorCronogramaVacunacion.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/Programas.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/ProgramasVacunacionHandlerTests.cs
git commit -m "feat(avicola): aplicacion del catalogo de programas de vacunacion"
```

---

### Task 4: Application de asignación y retiro del plan (TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioTareasVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/AsignacionPlan.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/AsignacionPlanVacunacionHandlerTests.cs`

**Interfaces:**
- Consumes: `Galpon`, `ProgramaVacunacion`, `TareaVacunacion` (Tasks 1-2); `IRepositorioGalpones` (SP5); `IRepositorioProgramasVacunacion` (Task 3); `IUnidadTrabajoGestionAvicola`, `IRegistroVuelo`, `NotFoundException`, `ConflictException`.
- Produces:
  - `IRepositorioTareasVacunacion`: `void Agregar(TareaVacunacion tarea)`; `Task<TareaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)`; `Task<IReadOnlyList<TareaVacunacion>> ListarPorGalponAsync(Guid galponId, CancellationToken ct = default)` (historial: todas las activas del galpón, cualquier estado); `Task<IReadOnlyList<TareaVacunacion>> ListarNotificacionAsync(Guid clienteId, DateOnly hoy, DateOnly hasta, CancellationToken ct = default)` (pendientes con `FechaProgramada <= hasta`); `Task<int> DesactivarPendientesDeGalponAsync(Guid galponId, CancellationToken ct = default)` (soft delete de las pendientes; devuelve cuántas).
  - DTOs: `TareaVacunacionResumen(Guid Id, Guid GalponId, int EdadDia, string Vacuna, string? ModoAplicacion, DateOnly FechaProgramada, string Estado, DateOnly? FechaAplicacion, int? AvesVacunadas, string? ObservacionesProgramadas, string? ObservacionesAplicacion, string? MotivoCancelacion)`; `NotificacionVacunacionResumen(IReadOnlyList<TareaVacunacionResumen> VencidasYHoy, IReadOnlyList<TareaVacunacionResumen> Proximas)`.
  - Commands: `AsignarPlanVacunacionCommand(Guid GalponId, Guid ProgramaId) : IRequest`; `QuitarPlanVacunacionCommand(Guid GalponId) : IRequest`.

- [ ] **Step 1: Escribir los tests que fallan**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/AsignacionPlanVacunacionHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class AsignacionPlanVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioProgramasVacunacion _programas = Substitute.For<IRepositorioProgramasVacunacion>();
    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private Galpon GalponDemo() => new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Hoy.AddDays(-30), null);

    private static ProgramaVacunacion ProgramaDemo()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", Hoy.AddDays(-60), 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
            new DatosItemPlanVacunacion(10, "NEWCASTLE", "Gota ocular", "Ayuno 2 horas"),
        ]);
        return programa;
    }

    private AsignarPlanVacunacionHandler HandlerAsignar() =>
        new(_galpones, _programas, _tareas, _vuelo, _unidad);

    [Fact]
    public async Task AsignarConGalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerAsignar().Handle(new(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
        _tareas.DidNotReceive().Agregar(Arg.Any<TareaVacunacion>());
    }

    [Fact]
    public async Task AsignarConProgramaInexistenteOInactivoLanzaNotFound()
    {
        var galpon = GalponDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerAsignar().Handle(new(galpon.Id, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Programa de vacunación no encontrado.", ex.Message);
    }

    [Fact]
    public async Task AsignarProgramaSinCronogramaLanzaConflict()
    {
        var galpon = GalponDemo();
        var programa = new ProgramaVacunacion("PLAN VACIO", Hoy.AddDays(-60), 1000, null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None));

        Assert.Equal("No se pudo asignar el plan de vacunación.", ex.Message);
        _tareas.DidNotReceive().Agregar(Arg.Any<TareaVacunacion>());
    }

    [Fact]
    public async Task AsignarCreaUnaTareaPorItemConFechaDesdeElNacimientoDelLote()
    {
        var galpon = GalponDemo();
        var programa = ProgramaDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

        await HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None);

        var nacimiento = Hoy.AddDays(-30);
        _tareas.Received(2).Agregar(Arg.Any<TareaVacunacion>());
        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.GalponId == galpon.Id && t.ClienteId == galpon.ClienteId
            && t.ProgramaVacunacionId == programa.Id
            && t.EdadDia == 3 && t.Vacuna == "BIO COCCIVET R" && t.ModoAplicacion == "Agua de bebida"
            && t.FechaProgramada == nacimiento.AddDays(3)
            && t.Estado == EstadoTareaVacunacion.Pendiente && t.EstaActivo));
        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.EdadDia == 10 && t.ObservacionesProgramadas == "Ayuno 2 horas"
            && t.FechaProgramada == nacimiento.AddDays(10)));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarDesactivaLasPendientesAnterioresYNarraElResultado()
    {
        var galpon = GalponDemo();
        var programa = ProgramaDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _tareas.DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(2);

        await HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None);

        await _tareas.Received(1).DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir("avicola.vacunacion.asignar", "asignacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c =>
                Equals(c["TareasCreadas"], 2) && Equals(c["TareasPendientesDesactivadas"], 2)));
    }

    [Fact]
    public async Task QuitarConGalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        var handler = new QuitarPlanVacunacionHandler(_galpones, _tareas, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task QuitarDesactivaLasPendientesYGuarda()
    {
        var galpon = GalponDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _tareas.DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(1);
        var handler = new QuitarPlanVacunacionHandler(_galpones, _tareas, _vuelo, _unidad);

        await handler.Handle(new(galpon.Id), CancellationToken.None);

        _vuelo.Received().Decidir("avicola.vacunacion.quitar-plan", "quitar", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => Equals(c["TareasPendientesDesactivadas"], 1)));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~AsignacionPlanVacunacionHandlerTests"`
Expected: FALLA la compilación (no existen los tipos).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioTareasVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Todas las consultas respetan los filtros globales (EstaActivo + tenant):
// un id ajeno o inactivo devuelve null/vacío, igual que uno inexistente
// (anti-enumeración). La desactivación de pendientes preserva completadas y
// canceladas: son el historial sanitario del lote (spec SP7).
public interface IRepositorioTareasVacunacion
{
    void Agregar(TareaVacunacion tarea);

    Task<TareaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Historial del galpón: todas las tareas activas, de cualquier estado,
    // ordenadas por fecha programada.
    Task<IReadOnlyList<TareaVacunacion>> ListarPorGalponAsync(Guid galponId, CancellationToken cancellationToken = default);

    // Notificación: pendientes con FechaProgramada <= hasta (las vencidas no
    // desaparecen). El clienteId se pasa explícito además del filtro global.
    Task<IReadOnlyList<TareaVacunacion>> ListarNotificacionAsync(
        Guid clienteId, DateOnly hoy, DateOnly hasta, CancellationToken cancellationToken = default);

    // Soft delete de las pendientes del galpón; devuelve cuántas se desactivaron.
    Task<int> DesactivarPendientesDeGalponAsync(Guid galponId, CancellationToken cancellationToken = default);
}

public sealed record TareaVacunacionResumen(
    Guid Id, Guid GalponId, int EdadDia, string Vacuna, string? ModoAplicacion,
    DateOnly FechaProgramada, string Estado, DateOnly? FechaAplicacion, int? AvesVacunadas,
    string? ObservacionesProgramadas, string? ObservacionesAplicacion, string? MotivoCancelacion);

public sealed record NotificacionVacunacionResumen(
    IReadOnlyList<TareaVacunacionResumen> VencidasYHoy,
    IReadOnlyList<TareaVacunacionResumen> Proximas);
```

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/AsignacionPlan.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Asignación (spec SP7): materializa una tarea por ítem activo del programa
// con snapshot y FechaProgramada = FechaNacimientoLote + EdadDia. Las
// pendientes del plan anterior se desactivan (soft delete); las completadas y
// canceladas quedan como historial. Un galpón tiene a lo sumo un plan
// vigente: se deriva de las tareas pendientes, sin campo extra en Galpon.
public sealed record AsignarPlanVacunacionCommand(Guid GalponId, Guid ProgramaId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.asignar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["TareasCreadas"] = DatoRegistroVuelo.Entero,
            ["TareasPendientesDesactivadas"] = DatoRegistroVuelo.Entero,
        });
}

public sealed record QuitarPlanVacunacionCommand(Guid GalponId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.quitar-plan",
        new Dictionary<string, DatoRegistroVuelo> { ["TareasPendientesDesactivadas"] = DatoRegistroVuelo.Entero });
}

public sealed class AsignarPlanVacunacionHandler(
    IRepositorioGalpones galpones, IRepositorioProgramasVacunacion programas,
    IRepositorioTareasVacunacion tareas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AsignarPlanVacunacionCommand>
{
    public async Task Handle(AsignarPlanVacunacionCommand request, CancellationToken cancellationToken)
    {
        // El filtro global garantiza galpón activo del tenant; id ajeno = 404.
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        // ObtenerPorIdAsync respeta EstaActivo: un programa inactivo no es asignable (404 genérico).
        var programa = await programas.ObtenerPorIdAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var items = programa.Items.Where(i => i.EstaActivo).ToList();
        if (items.Count == 0)
            throw new ConflictException("No se pudo asignar el plan de vacunación.");

        var desactivadas = await tareas.DesactivarPendientesDeGalponAsync(galpon.Id, cancellationToken);
        foreach (var item in items)
            tareas.Agregar(new TareaVacunacion(
                galpon.Id, galpon.ClienteId, programa.Id, item.Id,
                item.EdadDia, item.Vacuna, item.ModoAplicacion, item.Observaciones,
                galpon.FechaNacimientoLote.AddDays(item.EdadDia)));

        registroVuelo.Decidir("avicola.vacunacion.asignar", "asignacion", "aplicada",
            new Dictionary<string, object?>
            {
                ["TareasCreadas"] = items.Count,
                ["TareasPendientesDesactivadas"] = desactivadas,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuitarPlanVacunacionHandler(
    IRepositorioGalpones galpones, IRepositorioTareasVacunacion tareas,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<QuitarPlanVacunacionCommand>
{
    public async Task Handle(QuitarPlanVacunacionCommand request, CancellationToken cancellationToken)
    {
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        var desactivadas = await tareas.DesactivarPendientesDeGalponAsync(galpon.Id, cancellationToken);
        registroVuelo.Decidir("avicola.vacunacion.quitar-plan", "quitar", "aplicada",
            new Dictionary<string, object?> { ["TareasPendientesDesactivadas"] = desactivadas });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~AsignacionPlanVacunacionHandlerTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/IRepositorioTareasVacunacion.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/AsignacionPlan.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/AsignacionPlanVacunacionHandlerTests.cs
git commit -m "feat(avicola): asignacion y retiro del plan de vacunacion del galpon"
```

---

### Task 5: Application de completar y cancelar tareas (TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/EjecucionTareas.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/EjecucionTareasVacunacionHandlerTests.cs`

**Interfaces:**
- Consumes: `TareaVacunacion`, `EstadoTareaVacunacion` (Task 2); `IRepositorioTareasVacunacion` (Task 4); `ICurrentUser`, `IRegistroVuelo`, `IUnidadTrabajoGestionAvicola`, `NotFoundException`, `ConflictException`.
- Produces: `CompletarTareaVacunacionCommand(Guid TareaId, DateOnly? FechaAplicacion, int? AvesVacunadas, string? Observaciones) : IRequest`; `CancelarTareaVacunacionCommand(Guid TareaId, string? Motivo) : IRequest`; validators `CompletarTareaVacunacionValidator`, `CancelarTareaVacunacionValidator`.

- [ ] **Step 1: Escribir los tests que fallan**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/EjecucionTareasVacunacionHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EjecucionTareasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static TareaVacunacion TareaPendiente() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            3, "BIO COCCIVET R", null, null, Hoy);

    private CompletarTareaVacunacionHandler HandlerCompletar() => new(_tareas, _usuario, _vuelo, _unidad);

    [Fact]
    public async Task CompletarInexistenteLanzaNotFound()
    {
        _tareas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TareaVacunacion?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerCompletar().Handle(new(Guid.NewGuid(), null, null, null), CancellationToken.None));

        Assert.Equal("Tarea de vacunación no encontrada.", ex.Message);
    }

    [Fact]
    public async Task CompletarUsaHoyPorDefectoYRegistraElUsuarioActual()
    {
        var tarea = TareaPendiente();
        var usuarioId = Guid.NewGuid();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(usuarioId);

        await HandlerCompletar().Handle(new(tarea.Id, null, null, null), CancellationToken.None);

        Assert.Equal(EstadoTareaVacunacion.Completada, tarea.Estado);
        Assert.Equal(Hoy, tarea.FechaAplicacion);
        Assert.Equal(usuarioId, tarea.CompletadaPor);
        Assert.Null(tarea.AvesVacunadas);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletarConFechaPasadaYDetalleLosConserva()
    {
        var tarea = TareaPendiente();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(Guid.NewGuid());

        await HandlerCompletar().Handle(new(tarea.Id, Hoy.AddDays(-2), 950, "parcial"), CancellationToken.None);

        Assert.Equal(Hoy.AddDays(-2), tarea.FechaAplicacion);
        Assert.Equal(950, tarea.AvesVacunadas);
        _vuelo.Received().Decidir("avicola.vacunacion.completar", "aplicacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => Equals(c["AvesVacunadas"], 950)));
    }

    [Fact]
    public async Task CompletarTareaYaCerradaLanzaConflict()
    {
        var tarea = TareaPendiente();
        tarea.Completar(Hoy, null, Guid.NewGuid(), null);
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            HandlerCompletar().Handle(new(tarea.Id, null, null, null), CancellationToken.None));

        Assert.Equal("No se pudo completar la tarea de vacunación.", ex.Message);
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarInexistenteLanzaNotFound()
    {
        _tareas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TareaVacunacion?)null);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task CancelarPendienteRegistraMotivoYGuarda()
    {
        var tarea = TareaPendiente();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        await handler.Handle(new(tarea.Id, "Lote diezmado"), CancellationToken.None);

        Assert.Equal(EstadoTareaVacunacion.Cancelada, tarea.Estado);
        Assert.Equal("Lote diezmado", tarea.MotivoCancelacion);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarTareaYaCerradaLanzaConflict()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar(null);
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(tarea.Id, null), CancellationToken.None));

        Assert.Equal("No se pudo cancelar la tarea de vacunación.", ex.Message);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EjecucionTareasVacunacionHandlerTests"`
Expected: FALLA la compilación (no existen los tipos).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/EjecucionTareas.cs`:

```csharp
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Completar cierra algo que pudo ocurrir ayer (spec SP7): la fecha la informa
// el usuario (por defecto hoy, nunca futura — lo valida el dominio). La
// segunda llamada sobre la misma tarea es 409 por estado: la operación es
// naturalmente idempotente y no hace falta IdempotencyKey. CompletadaPor es
// el id del usuario actual, nunca el nombre (anti-PII).
public sealed record CompletarTareaVacunacionCommand(
    Guid TareaId, DateOnly? FechaAplicacion, int? AvesVacunadas, string? Observaciones)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.completar",
        new Dictionary<string, DatoRegistroVuelo> { ["AvesVacunadas"] = DatoRegistroVuelo.Entero });
}

public sealed record CancelarTareaVacunacionCommand(Guid TareaId, string? Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.cancelar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed class CompletarTareaVacunacionValidator : AbstractValidator<CompletarTareaVacunacionCommand>
{
    public CompletarTareaVacunacionValidator()
    {
        RuleFor(x => x.AvesVacunadas).GreaterThan(0).When(x => x.AvesVacunadas.HasValue);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class CancelarTareaVacunacionValidator : AbstractValidator<CancelarTareaVacunacionCommand>
{
    public CancelarTareaVacunacionValidator() => RuleFor(x => x.Motivo).MaximumLength(500);
}

public sealed class CompletarTareaVacunacionHandler(
    IRepositorioTareasVacunacion tareas, ICurrentUser usuario, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CompletarTareaVacunacionCommand>
{
    public async Task Handle(CompletarTareaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var tarea = await tareas.ObtenerPorIdAsync(request.TareaId, cancellationToken)
            ?? throw new NotFoundException("Tarea de vacunación", request.TareaId);
        if (tarea.Estado != EstadoTareaVacunacion.Pendiente)
            throw new ConflictException("No se pudo completar la tarea de vacunación.");
        tarea.Completar(
            request.FechaAplicacion ?? DateOnly.FromDateTime(DateTime.UtcNow),
            request.AvesVacunadas, usuario.UsuarioId ?? Guid.Empty, request.Observaciones);
        if (request.AvesVacunadas is int aves)
            registroVuelo.Decidir("avicola.vacunacion.completar", "aplicacion", "aplicada",
                new Dictionary<string, object?> { ["AvesVacunadas"] = aves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Cancelar es decisión de gestión (spec SP7): el endpoint la limita al rol
// Cliente; aquí solo importa el estado.
public sealed class CancelarTareaVacunacionHandler(
    IRepositorioTareasVacunacion tareas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CancelarTareaVacunacionCommand>
{
    public async Task Handle(CancelarTareaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var tarea = await tareas.ObtenerPorIdAsync(request.TareaId, cancellationToken)
            ?? throw new NotFoundException("Tarea de vacunación", request.TareaId);
        if (tarea.Estado != EstadoTareaVacunacion.Pendiente)
            throw new ConflictException("No se pudo cancelar la tarea de vacunación.");
        tarea.Cancelar(request.Motivo);
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EjecucionTareasVacunacionHandlerTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/EjecucionTareas.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/EjecucionTareasVacunacionHandlerTests.cs
git commit -m "feat(avicola): completar y cancelar tareas de vacunacion"
```

---

### Task 6: Application de consultas de tareas (historial y notificación, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/ConsultasTareas.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ConsultasTareasVacunacionHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGalpones` (SP5); `IRepositorioTareasVacunacion`, `TareaVacunacionResumen`, `NotificacionVacunacionResumen` (Task 4); `ICurrentUser`, `NotFoundException`.
- Produces: `ListarTareasPorGalponQuery(Guid GalponId) : IRequest<IReadOnlyList<TareaVacunacionResumen>>`; `ListarNotificacionVacunacionQuery() : IRequest<NotificacionVacunacionResumen>`.

- [ ] **Step 1: Escribir los tests que fallan**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/ConsultasTareasVacunacionHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ConsultasTareasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();

    private static TareaVacunacion Tarea(Guid galponId, Guid clienteId, DateOnly fechaProgramada, string vacuna) =>
        new(galponId, clienteId, Guid.NewGuid(), Guid.NewGuid(), 3, vacuna, null, null, fechaProgramada);

    [Fact]
    public async Task HistorialDeGalponAjenoLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        var handler = new ListarTareasPorGalponHandler(_galpones, _tareas);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
    }

    [Fact]
    public async Task HistorialDevuelveTodasLasTareasConSuEstado()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Hoy.AddDays(-30), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        var completada = Tarea(galpon.Id, galpon.ClienteId, Hoy.AddDays(-20), "A");
        completada.Completar(Hoy.AddDays(-20), null, Guid.NewGuid(), null);
        var pendiente = Tarea(galpon.Id, galpon.ClienteId, Hoy.AddDays(5), "B");
        _tareas.ListarPorGalponAsync(galpon.Id, Arg.Any<CancellationToken>())
            .Returns([pendiente, completada]);
        var handler = new ListarTareasPorGalponHandler(_galpones, _tareas);

        var historial = await handler.Handle(new(galpon.Id), CancellationToken.None);

        Assert.Equal(2, historial.Count);
        Assert.Equal("Completada", historial[0].Estado);
        Assert.Equal("Pendiente", historial[1].Estado);
    }

    [Fact]
    public async Task NotificacionSeparaVencidasYHoyDeLasProximas7Dias()
    {
        var clienteId = Guid.NewGuid();
        var galponId = Guid.NewGuid();
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        var vencida = Tarea(galponId, clienteId, Hoy.AddDays(-2), "VENCIDA");
        var deHoy = Tarea(galponId, clienteId, Hoy, "DE HOY");
        var proxima = Tarea(galponId, clienteId, Hoy.AddDays(5), "PROXIMA");
        _tareas.ListarNotificacionAsync(clienteId, Hoy, Hoy.AddDays(7), Arg.Any<CancellationToken>())
            .Returns([proxima, vencida, deHoy]);
        var handler = new ListarNotificacionVacunacionHandler(_tareas, _usuario);

        var notificacion = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(["VENCIDA", "DE HOY"], notificacion.VencidasYHoy.Select(t => t.Vacuna));
        Assert.Equal(["PROXIMA"], notificacion.Proximas.Select(t => t.Vacuna));
        await _tareas.Received(1).ListarNotificacionAsync(clienteId, Hoy, Hoy.AddDays(7), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ConsultasTareasVacunacionHandlerTests"`
Expected: FALLA la compilación (no existen los tipos).

- [ ] **Step 3: Implementación mínima**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/ConsultasTareas.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

public sealed record ListarTareasPorGalponQuery(Guid GalponId)
    : IRequest<IReadOnlyList<TareaVacunacionResumen>>;

public sealed record ListarNotificacionVacunacionQuery() : IRequest<NotificacionVacunacionResumen>;

// Historial sanitario del galpón (spec SP7): todas las tareas activas con su
// estado, ordenadas por fecha programada.
public sealed class ListarTareasPorGalponHandler(
    IRepositorioGalpones galpones, IRepositorioTareasVacunacion tareas)
    : IRequestHandler<ListarTareasPorGalponQuery, IReadOnlyList<TareaVacunacionResumen>>
{
    public async Task<IReadOnlyList<TareaVacunacionResumen>> Handle(
        ListarTareasPorGalponQuery request, CancellationToken cancellationToken)
    {
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        var lista = await tareas.ListarPorGalponAsync(galpon.Id, cancellationToken);
        return lista.OrderBy(t => t.FechaProgramada).Select(Mapear).ToList();
    }

    internal static TareaVacunacionResumen Mapear(TareaVacunacion t) => new(
        t.Id, t.GalponId, t.EdadDia, t.Vacuna, t.ModoAplicacion, t.FechaProgramada,
        t.Estado.ToString(), t.FechaAplicacion, t.AvesVacunadas,
        t.ObservacionesProgramadas, t.ObservacionesAplicacion, t.MotivoCancelacion);
}

// Notificación (spec SP7): pendientes con FechaProgramada <= hoy + 7 días.
// VencidasYHoy (FechaProgramada <= hoy) no desaparece hasta completarse o
// cancelarse; Proximas es (hoy, hoy + 7]. El filtro global de tenant acota al
// cliente actual; el clienteId explícito es defensa en profundidad.
public sealed class ListarNotificacionVacunacionHandler(
    IRepositorioTareasVacunacion tareas, ICurrentUser usuario)
    : IRequestHandler<ListarNotificacionVacunacionQuery, NotificacionVacunacionResumen>
{
    public async Task<NotificacionVacunacionResumen> Handle(
        ListarNotificacionVacunacionQuery request, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var lista = await tareas.ListarNotificacionAsync(
            usuario.ClienteId ?? Guid.Empty, hoy, hoy.AddDays(7), cancellationToken);
        return new NotificacionVacunacionResumen(
            lista.Where(t => t.FechaProgramada <= hoy)
                .OrderBy(t => t.FechaProgramada).Select(ListarTareasPorGalponHandler.Mapear).ToList(),
            lista.Where(t => t.FechaProgramada > hoy)
                .OrderBy(t => t.FechaProgramada).Select(ListarTareasPorGalponHandler.Mapear).ToList());
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ConsultasTareasVacunacionHandlerTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/ConsultasTareas.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/ConsultasTareasVacunacionHandlerTests.cs
git commit -m "feat(avicola): consultas de historial y notificacion de vacunacion"
```

---

### Task 7: Infraestructura (ClosedXML, importador, DbContext, configuraciones, repositorios, migración)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ImportadorCronogramaVacunacionTests.cs`
- Modify: `Icarus/Directory.Packages.props`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorCronogramaVacunacion.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionProgramaVacunacion.cs`, `ConfiguracionItemPlanVacunacion.cs`, `ConfiguracionTareaVacunacion.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioProgramasVacunacion.cs`, `RepositorioTareasVacunacion.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/` (generada por `dotnet ef`)

**Interfaces:**
- Consumes: todo lo de las Tasks 1-6; ClosedXML 0.105.0 (misma versión que el legacy `ICARUS.Web`).
- Produces: `ImportadorCronogramaVacunacion : IImportadorCronogramaVacunacion`; migración `Vacunacion`; repositorios e importador registrados en DI.

Nota de arquitectura (spec SP7): las reglas vigentes (`ReglasDeCapasTests`) prohíben librerías en **Domain** y referencias Infrastructure→Host / Application→Infrastructure. ClosedXML solo se usa en Infrastructure, que no tiene restricción de dependencias externas: **no hay que tocar las reglas de arquitectura**. `ReglasDeModulosTests` se verifica en la Task 10.

- [ ] **Step 1: Declarar ClosedXML y escribir el test del importador que falla**

En `Icarus/Directory.Packages.props`, dentro del segundo `<ItemGroup>` (junto a las demás `PackageVersion`):

```xml
    <PackageVersion Include="ClosedXML" Version="0.105.0" />
```

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj`, dentro del `<ItemGroup>` de paquetes:

```xml
    <PackageReference Include="ClosedXML" />
```

`Icarus/tests/Icarus.UnitTests/GestionAvicola/ImportadorCronogramaVacunacionTests.cs`:

```csharp
using ClosedXML.Excel;
using Icarus.GestionAvicola.Infrastructure.Importacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ImportadorCronogramaVacunacionTests
{
    private static readonly string[] EncabezadoCaisy =
        ["FECHA", "EDAD", "VACUNA", "MODO DE APLICACION", "OBSERVACIONES"];

    private static MemoryStream ExcelCon(string[] encabezados, string[][] filas)
    {
        var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        for (var c = 0; c < encabezados.Length; c++)
            hoja.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = filas[f][c];
        var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        memoria.Position = 0;
        return memoria;
    }

    [Fact]
    public void ExcelValidoDevuelveItemsIgnorandoLaColumnaFecha()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["09/10/2023", "3", "BIO COCCIVET R", "Agua de bebida", ""],
            ["16/10/2023", "10", "NEWCASTLE + BRONQUITIS", "Gota ocular", "Ayuno 2 horas"],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal(3, resultado.Items[0].EdadDia);
        Assert.Equal("BIO COCCIVET R", resultado.Items[0].Vacuna);
        Assert.Equal("Agua de bebida", resultado.Items[0].ModoAplicacion);
        Assert.Null(resultado.Items[0].Observaciones);
        Assert.Equal("Ayuno 2 horas", resultado.Items[1].Observaciones);
    }

    [Fact]
    public void EncabezadosConTildesMinusculasYEspaciosSeReconocen()
    {
        using var excel = ExcelCon(
            ["  fecha ", "edad día", "vacuna", "  modo   de aplicación ", "observaciones"],
            [["", "5", "GUMBORO", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        Assert.Single(resultado.Items);
        Assert.Equal(5, resultado.Items[0].EdadDia);
    }

    [Fact]
    public void FilaSinEdadSeReportaPorNumeroDeFila()
    {
        using var excel = ExcelCon(EncabezadoCaisy, [["", "", "GUMBORO", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(2, error.Fila);
        Assert.Contains("edad", error.Mensaje);
        Assert.Empty(resultado.Items);
    }

    [Fact]
    public void EdadRepetidaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["", "3", "A", "", ""],
            ["", "3", "B", "", ""],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(3, error.Fila);
        Assert.Contains("repetida", error.Mensaje);
        Assert.Single(resultado.Items);
    }

    [Fact]
    public void FilaSinVacunaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy, [["", "7", " ", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(2, error.Fila);
        Assert.Contains("vacuna", error.Mensaje);
    }

    [Fact]
    public void EncabezadoSinColumnasRequeridasSeReporta()
    {
        using var excel = ExcelCon(["FECHA", "OBSERVACIONES"], [["x", "y"]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(1, error.Fila);
        Assert.Empty(resultado.Items);
    }

    [Fact]
    public void HojaSinFilasDeCronogramaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy, []);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Single(resultado.Errores);
        Assert.Empty(resultado.Items);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ImportadorCronogramaVacunacionTests"`
Expected: FALLA la compilación (`ImportadorCronogramaVacunacion` no existe).

- [ ] **Step 3: Implementar el importador**

`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorCronogramaVacunacion.cs`:

```csharp
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Icarus.GestionAvicola.Application.Vacunacion;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Parseo tolerante del Excel del papel de CAISY (spec SP7): columnas FECHA
// (se ignora: la fuente de verdad es EDAD), EDAD, VACUNA, MODO DE APLICACION
// y OBSERVACIONES, con nombres tolerantes a mayúsculas, tildes y espacios.
// No decide el todo-o-nada: devuelve ítems y errores por fila; el handler
// rechaza la importación completa si hay errores.
public sealed class ImportadorCronogramaVacunacion : IImportadorCronogramaVacunacion
{
    public ResultadoImportacionCronograma Importar(Stream contenido)
    {
        var items = new List<ItemCronogramaImportado>();
        var errores = new List<ErrorFilaImportacion>();
        using var libro = new XLWorkbook(contenido);
        var hoja = libro.Worksheets.FirstOrDefault();
        if (hoja is null)
        {
            errores.Add(new ErrorFilaImportacion(1, "El archivo no contiene hojas de cálculo."));
            return new ResultadoImportacionCronograma(items, errores);
        }

        var columnas = IndicesColumnas(hoja.Row(1));
        if (columnas.Edad is null || columnas.Vacuna is null)
        {
            errores.Add(new ErrorFilaImportacion(1, "Faltan las columnas EDAD y VACUNA en el encabezado."));
            return new ResultadoImportacionCronograma(items, errores);
        }

        var edadesVistas = new HashSet<int>();
        var fila = 2;
        while (!hoja.Row(fila).IsEmpty())
        {
            var edadTexto = TextoCelda(hoja, fila, columnas.Edad);
            var vacuna = TextoCelda(hoja, fila, columnas.Vacuna);
            var modo = TextoCelda(hoja, fila, columnas.ModoAplicacion);
            var observaciones = TextoCelda(hoja, fila, columnas.Observaciones);

            var valida = true;
            var edad = 0;
            if (!int.TryParse(edadTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out edad) || edad <= 0)
            {
                errores.Add(new ErrorFilaImportacion(fila, "La edad debe ser un número entero mayor que cero."));
                valida = false;
            }
            else if (!edadesVistas.Add(edad))
            {
                errores.Add(new ErrorFilaImportacion(fila, $"La edad {edad} está repetida en el archivo."));
                valida = false;
            }
            if (string.IsNullOrWhiteSpace(vacuna))
            {
                errores.Add(new ErrorFilaImportacion(fila, "La vacuna es obligatoria."));
                valida = false;
            }

            if (valida)
                items.Add(new ItemCronogramaImportado(
                    edad, vacuna.Trim(),
                    string.IsNullOrWhiteSpace(modo) ? null : modo.Trim(),
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim()));
            fila++;
        }

        if (items.Count == 0 && errores.Count == 0)
            errores.Add(new ErrorFilaImportacion(1, "El archivo no contiene filas de cronograma."));
        return new ResultadoImportacionCronograma(items, errores);
    }

    // La columna FECHA se ignora (spec SP7): se deriva de la entrada del lote
    // más la EdadDia al asignar.
    private static (int? Edad, int? Vacuna, int? ModoAplicacion, int? Observaciones) IndicesColumnas(IXLRow encabezado)
    {
        int? edad = null, vacuna = null, modo = null, observaciones = null;
        foreach (var celda in encabezado.CellsUsed())
        {
            var nombre = Normalizar(celda.GetString());
            if (nombre.StartsWith("EDAD", StringComparison.Ordinal))
                edad = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("VACUNA", StringComparison.Ordinal))
                vacuna = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("MODO", StringComparison.Ordinal))
                modo = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("OBSERVACIONES", StringComparison.Ordinal))
                observaciones = celda.Address.ColumnNumber;
        }
        return (edad, vacuna, modo, observaciones);
    }

    private static string TextoCelda(IXLWorksheet hoja, int fila, int? columna) =>
        columna is null ? string.Empty : hoja.Cell(fila, columna.Value).GetString().Trim();

    private static string Normalizar(string texto)
    {
        var constructor = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                constructor.Append(char.ToUpperInvariant(c));
        return string.Join(' ', constructor.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde del importador**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ImportadorCronogramaVacunacionTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: DbContext, configuraciones EF, repositorios y DI**

En `GestionAvicolaDbContext.cs`, añadir los DbSet junto a los existentes:

```csharp
    public DbSet<ProgramaVacunacion> ProgramasVacunacion => Set<ProgramaVacunacion>();
    public DbSet<ItemPlanVacunacion> ItemsPlanVacunacion => Set<ItemPlanVacunacion>();
    public DbSet<TareaVacunacion> TareasVacunacion => Set<TareaVacunacion>();
```

Y en `OnModelCreating`, junto a los filtros existentes (misma regla: sin `.Value` sobre el nullable):

```csharp
        // Catálogo global (spec SP7): sin filtro de tenant, solo EstaActivo.
        modelBuilder.Entity<ProgramaVacunacion>().HasQueryFilter(p => p.EstaActivo);
        modelBuilder.Entity<ItemPlanVacunacion>().HasQueryFilter(i => i.EstaActivo);
        modelBuilder.Entity<TareaVacunacion>().HasQueryFilter(t =>
            t.EstaActivo && (_clienteIdActual == null || t.ClienteId == _clienteIdActual));
```

`Persistencia/ConfiguracionProgramaVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionProgramaVacunacion : IEntityTypeConfiguration<ProgramaVacunacion>
{
    public void Configure(EntityTypeBuilder<ProgramaVacunacion> builder)
    {
        builder.ToTable("programas_vacunacion", t =>
            t.HasCheckConstraint("CK_programas_vacunacion_cantidad_aves", "[CantidadAves] > 0"));
        builder.Property(p => p.Nombre).HasMaxLength(200);
        builder.Property(p => p.Observaciones).HasMaxLength(1000);
        builder.Property(p => p.FechaEmision).HasColumnType("date");
        // Unicidad incluyendo inactivos (spec SP7): el soft delete no libera el nombre.
        builder.HasIndex(p => p.Nombre).IsUnique();
        builder.HasMany(p => p.Items).WithOne().HasForeignKey("ProgramaVacunacionId");
        builder.Navigation(p => p.Items).HasField("_items");
    }
}
```

`Persistencia/ConfiguracionItemPlanVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionItemPlanVacunacion : IEntityTypeConfiguration<ItemPlanVacunacion>
{
    public void Configure(EntityTypeBuilder<ItemPlanVacunacion> builder)
    {
        builder.ToTable("programas_vacunacion_items", t =>
            t.HasCheckConstraint("CK_programas_vacunacion_items_edad", "[EdadDia] > 0"));
        builder.Property(i => i.Vacuna).HasMaxLength(200);
        builder.Property(i => i.ModoAplicacion).HasMaxLength(500);
        builder.Property(i => i.Observaciones).HasMaxLength(1000);
        builder.HasIndex("ProgramaVacunacionId");
    }
}
```

`Persistencia/ConfiguracionTareaVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionTareaVacunacion : IEntityTypeConfiguration<TareaVacunacion>
{
    public void Configure(EntityTypeBuilder<TareaVacunacion> builder)
    {
        builder.ToTable("tareas_vacunacion", t =>
        {
            // Las invariantes del agregado, como última línea de defensa en BD.
            t.HasCheckConstraint("CK_tareas_vacunacion_edad", "[EdadDia] > 0");
            t.HasCheckConstraint("CK_tareas_vacunacion_aves", "[AvesVacunadas] IS NULL OR [AvesVacunadas] > 0");
            t.HasCheckConstraint("CK_tareas_vacunacion_estado_fecha",
                "[Estado] <> 'Completada' OR [FechaAplicacion] IS NOT NULL");
        });
        builder.Property(t => t.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Vacuna).HasMaxLength(200);
        builder.Property(t => t.ModoAplicacion).HasMaxLength(500);
        builder.Property(t => t.ObservacionesProgramadas).HasMaxLength(1000);
        builder.Property(t => t.ObservacionesAplicacion).HasMaxLength(1000);
        builder.Property(t => t.MotivoCancelacion).HasMaxLength(500);
        builder.Property(t => t.FechaProgramada).HasColumnType("date");
        builder.Property(t => t.FechaAplicacion).HasColumnType("date");
        builder.HasIndex(t => new { t.ClienteId, t.FechaProgramada });
        builder.HasIndex(t => new { t.GalponId, t.Estado });
    }
}
```

`Repositorios/RepositorioProgramasVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioProgramasVacunacion(GestionAvicolaDbContext db) : IRepositorioProgramasVacunacion
{
    public void Agregar(ProgramaVacunacion programa) => db.ProgramasVacunacion.Add(programa);

    public async Task<ProgramaVacunacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // Rol de plataforma (spec SP7): el Administrador gestiona el catálogo
    // completo, incluidos los inactivos.
    public async Task<ProgramaVacunacion?> ObtenerPorIdIncluyendoInactivosAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters().Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<bool> ExisteNombreAsync(
        string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters()
            .AnyAsync(p => p.Nombre == nombre && (excluyendoId == null || p.Id != excluyendoId), cancellationToken);

    public async Task<IReadOnlyList<ProgramaVacunacion>> ListarAsync(
        bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var consulta = incluirInactivos
            ? db.ProgramasVacunacion.IgnoreQueryFilters()
            : db.ProgramasVacunacion;
        return await consulta.OrderBy(p => p.Nombre).ToListAsync(cancellationToken);
    }
}
```

`Repositorios/RepositorioTareasVacunacion.cs`:

```csharp
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioTareasVacunacion(GestionAvicolaDbContext db) : IRepositorioTareasVacunacion
{
    public void Agregar(TareaVacunacion tarea) => db.TareasVacunacion.Add(tarea);

    public async Task<TareaVacunacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TareaVacunacion>> ListarPorGalponAsync(
        Guid galponId, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion.Where(t => t.GalponId == galponId)
            .OrderBy(t => t.FechaProgramada).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TareaVacunacion>> ListarNotificacionAsync(
        Guid clienteId, DateOnly hoy, DateOnly hasta, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion
            .Where(t => t.ClienteId == clienteId
                && t.Estado == EstadoTareaVacunacion.Pendiente && t.FechaProgramada <= hasta)
            .OrderBy(t => t.FechaProgramada).ToListAsync(cancellationToken);

    // Soft delete vía el agregado (tracked): el historial completado/cancelado
    // no se toca (spec SP7). El filtro global ya excluye las desactivadas.
    public async Task<int> DesactivarPendientesDeGalponAsync(
        Guid galponId, CancellationToken cancellationToken = default)
    {
        var pendientes = await db.TareasVacunacion
            .Where(t => t.GalponId == galponId && t.Estado == EstadoTareaVacunacion.Pendiente)
            .ToListAsync(cancellationToken);
        foreach (var tarea in pendientes)
            tarea.Desactivar();
        return pendientes.Count;
    }
}
```

En `DependencyInjection.cs` (`AddGestionAvicolaInfraestructura`), añadir los usings de `Icarus.GestionAvicola.Application.Vacunacion` e `Icarus.GestionAvicola.Infrastructure.Importacion`, y junto a los registros existentes:

```csharp
        servicios.AddScoped<IRepositorioProgramasVacunacion, RepositorioProgramasVacunacion>();
        servicios.AddScoped<IRepositorioTareasVacunacion, RepositorioTareasVacunacion>();
        servicios.AddScoped<IImportadorCronogramaVacunacion, ImportadorCronogramaVacunacion>();
```

- [ ] **Step 6: Generar la migración**

```bash
cd Icarus && dotnet tool restore
dotnet ef migrations add Vacunacion \
  --project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --startup-project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --context GestionAvicolaDbContext
```

Expected: migración generada con las tablas `gestion_avicola.programas_vacunacion`, `gestion_avicola.programas_vacunacion_items` y `gestion_avicola.tareas_vacunacion`; índice único `(Nombre)` sin filtro; índices `(ClienteId, FechaProgramada)` y `(GalponId, Estado)`; checks `CK_*`; `FechaEmision`/`FechaProgramada`/`FechaAplicacion` tipo `date`; `Estado` como texto. Revisar el archivo generado antes de seguir (comportamiento de borrado de ítems: cascade desde el programa).

- [ ] **Step 7: Verificar build y tests del módulo**

Run: `dotnet build Icarus/Icarus.sln --nologo` y `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GestionAvicola"`
Expected: BUILD succeeded, 0 warnings; tests PASS (incluidas las Tasks 1-6 y el importador).

- [ ] **Step 8: Commit**

```bash
git add Icarus/Directory.Packages.props Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure Icarus/tests/Icarus.UnitTests/GestionAvicola/ImportadorCronogramaVacunacionTests.cs
git commit -m "feat(avicola): persistencia e importacion excel de vacunacion con migracion"
```

---

### Task 8: Entitlement backend y endpoints del Host

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/ManejadorCatalogoVacunacionTests.cs`
- Modify (test): `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoCatalogoVacunacion.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/PoliticasClientes.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`

**Interfaces:**
- Consumes: commands/queries de las Tasks 3-6; `PoliticasClientes.Para(...)`; `ClaimsIdentidad.Rol` / `Rol` (Identity).
- Produces:
  - `FuncionalidadesTrabajador.Asignables = ProduccionHuevos | Mortalidad | Vacunacion`; `EsAsignable` incluye `Vacunacion`. Los valores numéricos del enum NO cambian (compatibilidad: `Vacunacion = 16` ya existe).
  - `PoliticasClientes.CatalogoVacunacion = "CatalogoVacunacion"`: política que pasa con la funcionalidad `Vacunacion` (cliente con el módulo o trabajador asignado) **o** con el claim de rol Administrador.
  - `PoliticasAutorizacion.SoloCliente = "SoloCliente"`: claim `rol = Cliente` (para cancelar: rol Cliente + módulo vía AND con `Funcionalidad:Vacunacion`).
  - Política `GestionAvicolaEstructura` con OR ampliado a `ProduccionHuevos | Mortalidad | Vacunacion`.
  - Endpoints según la tabla del spec (ver Step 4).

- [ ] **Step 1: Escribir los tests que fallan**

En `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs`, mover `Vacunacion` de la teoría negativa a la positiva:

- En `SoloFuncionalidadesOperativasSonAsignables` agregar `[InlineData(Funcionalidades.Vacunacion)]` (queda ProduccionHuevos, Mortalidad, Vacunacion).
- En `FuncionalidadesEstructuralesYFuturasNoSonAsignables` quitar `[InlineData(Funcionalidades.Vacunacion)]` (quedan Granjas, Galpones, Alimentacion, Despachos, Precios).
- Agregar al final de la clase:

```csharp
    [Fact]
    public void AsignablesIncluyeProduccionMortalidadYVacunacion()
    {
        Assert.Equal(
            Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad | Funcionalidades.Vacunacion,
            FuncionalidadesTrabajador.Asignables);
    }
```

`Icarus/tests/Icarus.UnitTests/Clientes/ManejadorCatalogoVacunacionTests.cs`:

```csharp
using System.Security.Claims;
using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class ManejadorCatalogoVacunacionTests
{
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IVerificadorEntitlement _entitlement = Substitute.For<IVerificadorEntitlement>();

    private async Task<bool> Autoriza()
    {
        var requisito = new RequisitoCatalogoVacunacion();
        var contexto = new AuthorizationHandlerContext([requisito], new ClaimsPrincipal(), null);
        await new ManejadorCatalogoVacunacion(_usuario, _entitlement).HandleAsync(contexto);
        return contexto.HasSucceeded;
    }

    [Fact]
    public async Task AdministradorSinClientePasa()
    {
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Administrador");

        Assert.True(await Autoriza());
    }

    [Fact]
    public async Task ClienteConElModuloPasa()
    {
        var clienteId = Guid.NewGuid();
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Cliente");
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        _entitlement.TieneFuncionalidadAsync(clienteId, null, Funcionalidades.Vacunacion, Arg.Any<CancellationToken>())
            .Returns(true);

        Assert.True(await Autoriza());
    }

    [Fact]
    public async Task TrabajadorSinLaFuncionalidadNoPasa()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Trabajador");
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        _usuario.TrabajadorId.Returns<Guid?>(trabajadorId);
        _entitlement.TieneFuncionalidadAsync(clienteId, trabajadorId, Funcionalidades.Vacunacion, Arg.Any<CancellationToken>())
            .Returns(false);

        Assert.False(await Autoriza());
    }

    [Fact]
    public async Task NoAutenticadoNoPasa()
    {
        _usuario.EstaAutenticado.Returns(false);

        Assert.False(await Autoriza());
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~FuncionalidadesTests|FullyQualifiedName~ManejadorCatalogoVacunacionTests"`
Expected: FALLA la compilación (`RequisitoCatalogoVacunacion`/`ManejadorCatalogoVacunacion` no existen) y, una vez compilando tras el Step 3 parcial, `Vacunacion` aún no es asignable.

- [ ] **Step 3: Implementar entitlement y políticas**

`Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs` (archivo completo):

```csharp
namespace Icarus.Clientes.Domain;

public static class FuncionalidadesTrabajador
{
    public const Funcionalidades Asignables =
        Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad | Funcionalidades.Vacunacion;

    public static bool EsAsignable(Funcionalidades funcionalidad) =>
        funcionalidad is Funcionalidades.ProduccionHuevos or Funcionalidades.Mortalidad or Funcionalidades.Vacunacion;
}
```

`Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoCatalogoVacunacion.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Lectura del catálogo global de programas de vacunación (spec SP7): la pasa
// quien tiene la funcionalidad Vacunacion (cliente por el módulo, trabajador
// por asignación) o el rol de plataforma que lo gestiona. El nombre del rol
// es contrato del JWT: Clientes no referencia Identity (regla de módulos).
public sealed class RequisitoCatalogoVacunacion : IAuthorizationRequirement
{
}

public sealed class ManejadorCatalogoVacunacion : AuthorizationHandler<RequisitoCatalogoVacunacion>
{
    private readonly ICurrentUser _usuario;
    private readonly IVerificadorEntitlement _entitlement;

    public ManejadorCatalogoVacunacion(ICurrentUser usuario, IVerificadorEntitlement entitlement)
    {
        _usuario = usuario;
        _entitlement = entitlement;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoCatalogoVacunacion requisito)
    {
        if (!_usuario.EstaAutenticado)
            return;
        if (string.Equals(_usuario.Rol, "Administrador", StringComparison.Ordinal))
        {
            context.Succeed(requisito);
            return;
        }
        if (_usuario.ClienteId is not { } clienteId)
            return;
        var cancelacion = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        if (await _entitlement.TieneFuncionalidadAsync(
                clienteId, _usuario.TrabajadorId, Funcionalidades.Vacunacion, cancelacion))
            context.Succeed(requisito);
    }
}
```

En `PoliticasClientes.cs`, junto a `Prefijo`:

```csharp
    // Lectura del catálogo de vacunación: funcionalidad Vacunacion o rol de
    // plataforma (spec SP7). Se registra en AddClientesInfraestructura.
    public const string CatalogoVacunacion = "CatalogoVacunacion";
```

En `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs`:

1. Junto a los demás handlers: `servicios.AddScoped<IAuthorizationHandler, ManejadorCatalogoVacunacion>();`
2. En `politicasEstructura`, ampliar el OR de `GestionAvicolaEstructura` y registrar la nueva política (método completo resultante):

```csharp
    private static void politicasEstructura(IServiceCollection servicios)
    {
        servicios.AddAuthorizationBuilder().AddPolicy("GestionAvicolaEstructura", builder => builder
            .RequireAuthenticatedUser()
            .AddRequirements(new RequisitoAlgunaFuncionalidadHabilitada(
                Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad | Funcionalidades.Vacunacion)));
        servicios.AddAuthorizationBuilder().AddPolicy(PoliticasClientes.CatalogoVacunacion, builder => builder
            .RequireAuthenticatedUser()
            .AddRequirements(new RequisitoCatalogoVacunacion()));
    }
```

En `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`, junto a las constantes existentes:

```csharp
    // Operaciones de gestión que el trabajador no ejecuta aunque tenga la
    // funcionalidad (spec SP7: cancelar tareas de vacunación).
    public const string SoloCliente = "SoloCliente";
```

En `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs`, en la cadena `AddAuthorizationBuilder()`:

```csharp
            .AddPolicy(PoliticasAutorizacion.SoloCliente,
                politica => politica.RequireClaim(ClaimsIdentidad.Rol, nameof(Rol.Cliente)));
```

- [ ] **Step 4: Endpoints de vacunación en el Host**

En `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`:

1. Agregar usings: `using Icarus.GestionAvicola.Application.Vacunacion;` y `using Icarus.Identity.Infrastructure.Autenticacion;`.
2. Al inicio de `MapGestionAvicola`, junto a las demás políticas: `var politicaVacunacion = PoliticasClientes.Para(Funcionalidades.Vacunacion);`
3. Antes del `return app;`, agregar los endpoints (autorización según la tabla del spec):

```csharp
        // Catálogo global de programas (spec SP7): escritura solo
        // Administrador; lectura con la política CatalogoVacunacion
        // (funcionalidad Vacunacion o rol de plataforma).
        var programasVacunacion = app.MapGroup("/vacunacion/programas");
        programasVacunacion.MapPost("/", async (CrearProgramaVacunacionRequest c, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearProgramaVacunacionCommand(c.Nombre, c.FechaEmision, c.CantidadAves, c.Observaciones));
            return Results.Created($"/vacunacion/programas/{id}", new { id });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        programasVacunacion.MapGet("/", async (bool? incluirInactivos, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarProgramasVacunacionQuery(incluirInactivos ?? false))))
            .RequireAuthorization(PoliticasClientes.CatalogoVacunacion);
        programasVacunacion.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerProgramaVacunacionQuery(id))))
            .RequireAuthorization(PoliticasClientes.CatalogoVacunacion);
        programasVacunacion.MapPut("/{id:guid}", async (Guid id, ActualizarProgramaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new ActualizarProgramaVacunacionCommand(id, c.Nombre, c.FechaEmision, c.CantidadAves, c.Observaciones));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        programasVacunacion.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarProgramaVacunacionCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        // El Excel reemplaza el cronograma completo (todo-o-nada); multipart
        // sin antiforgery: la autenticación es Bearer, no cookie.
        programasVacunacion.MapPost("/{id:guid}/cronograma-excel", async (Guid id, IFormFile archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            await using var contenido = archivo.OpenReadStream();
            var importados = await mediator.Send(new ImportarCronogramaExcelCommand(id, contenido), cancellationToken);
            return Results.Ok(new { itemsImportados = importados });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador).DisableAntiforgery();

        // Asignar/quitar plan: decisión estructural del cliente (los
        // trabajadores nunca tienen la funcionalidad Galpones).
        galpones.MapPost("/{galponId:guid}/plan-vacunacion", async (Guid galponId, AsignarPlanVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new AsignarPlanVacunacionCommand(galponId, c.ProgramaId));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);
        galpones.MapDelete("/{galponId:guid}/plan-vacunacion", async (Guid galponId, ISender mediator) =>
        {
            await mediator.Send(new QuitarPlanVacunacionCommand(galponId));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);
        galpones.MapGet("/{galponId:guid}/vacunacion/tareas", async (Guid galponId, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarTareasPorGalponQuery(galponId))))
            .RequireAuthorization(politicaVacunacion);

        var vacunacion = app.MapGroup("/vacunacion");
        vacunacion.MapGet("/tareas", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarNotificacionVacunacionQuery())))
            .RequireAuthorization(politicaVacunacion);
        vacunacion.MapPost("/tareas/{id:guid}/completar", async (Guid id, CompletarTareaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new CompletarTareaVacunacionCommand(id, c.FechaAplicacion, c.AvesVacunadas, c.Observaciones));
            return Results.NoContent();
        }).RequireAuthorization(politicaVacunacion);
        // Cancelar: solo cliente (AND de las dos políticas: rol Cliente +
        // funcionalidad Vacunacion del módulo).
        vacunacion.MapPost("/tareas/{id:guid}/cancelar", async (Guid id, CancelarTareaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new CancelarTareaVacunacionCommand(id, c.Motivo));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloCliente, politicaVacunacion);
```

4. Al final de la clase, junto a los demás bodies:

```csharp
    private sealed record CrearProgramaVacunacionRequest(string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones);
    private sealed record ActualizarProgramaVacunacionRequest(string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones);
    private sealed record AsignarPlanVacunacionRequest(Guid ProgramaId);
    private sealed record CompletarTareaVacunacionRequest(DateOnly? FechaAplicacion, int? AvesVacunadas, string? Observaciones);
    private sealed record CancelarTareaVacunacionRequest(string? Motivo);
```

- [ ] **Step 5: Ejecutar y ver el verde**

Run: `dotnet build Icarus/Icarus.sln --nologo` y `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~FuncionalidadesTests|FullyQualifiedName~ManejadorCatalogoVacunacionTests"`
Expected: BUILD succeeded, 0 warnings; tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs Icarus/src/Clientes/Icarus.Clientes.Infrastructure Icarus/src/Identity/Icarus.Identity.Infrastructure Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs Icarus/tests/Icarus.UnitTests/Clientes
git commit -m "feat(avicola): endpoints y entitlement de vacunacion"
```

---

### Task 9: Semilla demo del programa de vacunación

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaGestionAvicola.cs`

**Interfaces:**
- Consumes: `ProgramaVacunacion`, `DatosItemPlanVacunacion` (Task 1).
- Produces: `SemillaGestionAvicola.ProgramaVacunacionDemoId`. El Host ya invoca `SembrarAsync` solo en Development/Testing; no se toca `Program.cs`.

- [ ] **Step 1: Reescribir `SemillaGestionAvicola.cs` (archivo completo)**

```csharp
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.GestionAvicola.Infrastructure;

public static class SemillaGestionAvicola
{
    public static readonly Guid GranjaDemoId = new("aa000000-0000-0000-0000-000000000001");
    public static readonly Guid GalponDemoNorteId = new("aa000000-0000-0000-0000-000000000011");
    public static readonly Guid GalponDemoSurId = new("aa000000-0000-0000-0000-000000000012");
    public static readonly Guid ProgramaVacunacionDemoId = new("aa000000-0000-0000-0000-000000000021");

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        if (!await db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.Id == GranjaDemoId))
        {
            db.Granjas.Add(new Granja(GranjaDemoId, clienteDemoId, "Granja Demo"));
            db.Galpones.Add(new Galpon(GalponDemoNorteId, GranjaDemoId, clienteDemoId, "1", 5000, 4800, new DateOnly(2025, 9, 1), "Galpón norte"));
            db.Galpones.Add(new Galpon(GalponDemoSurId, GranjaDemoId, clienteDemoId, "2", 5000, 5000, new DateOnly(2026, 2, 2), null));
        }
        // Programa demo global (sin tenant), estilo del papel real de CAISY:
        // vacunas y manejos por igual (spec SP7).
        if (!await db.ProgramasVacunacion.IgnoreQueryFilters().AnyAsync(p => p.Id == ProgramaVacunacionDemoId))
        {
            var programa = new ProgramaVacunacion(
                ProgramaVacunacionDemoId, "PROGRAMA DE VACUNACION PARA 1000 AVES (DEMO)",
                new DateOnly(2026, 1, 15), 1000, "Plan de demostración estilo CAISY.");
            programa.ReemplazarCronograma([
                new DatosItemPlanVacunacion(1, "NEWCASTLE + BRONQUITIS", "Gota ocular", null),
                new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
                new DatosItemPlanVacunacion(10, "GUMBORO", "Agua de bebida", "Ayuno de agua 2 horas"),
                new DatosItemPlanVacunacion(18, "GUMBORO refuerzo", "Agua de bebida", null),
                new DatosItemPlanVacunacion(30, "Desparasitación", "Agua de bebida", null),
            ]);
            db.ProgramasVacunacion.Add(programa);
        }
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Verificar build**

Run: `dotnet build Icarus/Icarus.sln --nologo`
Expected: BUILD succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaGestionAvicola.cs
git commit -m "feat(avicola): semilla demo del programa de vacunacion"
```

---

### Task 10: Tests de integración (Testcontainers)

**Files:**
- Test: `Icarus/tests/Icarus.IntegrationTests/VacunacionEndpointsTests.cs`

**Interfaces:**
- Consumes: endpoints de la Task 8; semilla `SemillaIdentidad` (emails fijos); helpers del patrón `EntitlementTests` (replicados locales: la clase no los comparte).
- Docker corriendo (Testcontainers.MsSql), igual que toda la suite de integración.

- [ ] **Step 1: Escribir los tests que fallan (ya en verde contra la Task 8; el rojo se verifica si se corre antes de la 8)**

`Icarus/tests/Icarus.IntegrationTests/VacunacionEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// Flujo de vacunación de punta a punta (spec SP7): el Administrador gestiona
// el catálogo global, el cliente asigna/quita planes y cancela, el trabajador
// con la funcionalidad Vacunacion ve la notificación y completa.
[Collection(IntegracionCollection.Nombre)]
public class VacunacionEndpointsTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly IdentityFactory _factory;

    public VacunacionEndpointsTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Autenticado(HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        var pedido = new HttpRequestMessage(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        if (cuerpo is not null) pedido.Content = JsonContent.Create(cuerpo);
        return pedido;
    }

    private async Task<(Guid ClienteId, string Token)> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        using var cliente = _factory.CreateClient();
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Autenticado(HttpMethod.Put, $"/clientes/{id}/modulos", admin,
            new { modulos = new[] { "GestionAvicola" } }));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (id, await LoginComo(email));
    }

    private async Task<string> CrearTrabajador(Guid clienteId, string[] funcionalidades, string tokenCliente)
    {
        using var cliente = _factory.CreateClient();
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, $"/clientes/{clienteId}/trabajadores",
            tokenCliente, new
            {
                nombre = "Nombre Ficticio",
                documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Operario",
                fechaIngreso = "2026-01-15",
                email,
                contrasena = IdentityFactory.ContrasenaDePrueba,
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Put,
            $"/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            new { funcionalidades }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);
        return await LoginComo(email);
    }

    private async Task<Guid> CrearGalpon(string tokenCliente, DateOnly nacimientoLote)
    {
        using var cliente = _factory.CreateClient();
        var granja = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/granjas", tokenCliente,
            new { nombre = $"Granja {Guid.NewGuid():N}" }));
        Assert.Equal(HttpStatusCode.Created, granja.StatusCode);
        var granjaId = (await granja.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var galpon = await cliente.SendAsync(Autenticado(HttpMethod.Post, $"/granjas/{granjaId}/galpones",
            tokenCliente, new
            {
                numero = "1", capacidadMaxima = 5000, gallinasActuales = 1000,
                fechaNacimientoLote = nacimientoLote.ToString("yyyy-MM-dd"),
            }));
        Assert.Equal(HttpStatusCode.Created, galpon.StatusCode);
        return (await galpon.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent ExcelCronograma(params (int Edad, string Vacuna)[] items)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        hoja.Cell(1, 1).Value = "FECHA";
        hoja.Cell(1, 2).Value = "EDAD";
        hoja.Cell(1, 3).Value = "VACUNA";
        hoja.Cell(1, 4).Value = "MODO DE APLICACION";
        hoja.Cell(1, 5).Value = "OBSERVACIONES";
        var fila = 2;
        foreach (var (edad, vacuna) in items)
        {
            hoja.Cell(fila, 2).Value = edad;
            hoja.Cell(fila, 3).Value = vacuna;
            fila++;
        }
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var contenido = new MultipartFormDataContent();
        contenido.Add(new ByteArrayContent(memoria.ToArray()), "archivo", "cronograma.xlsx");
        return contenido;
    }

    private async Task<Guid> CrearProgramaConCronograma(string admin, params (int Edad, string Vacuna)[] items)
    {
        using var cliente = _factory.CreateClient();
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/vacunacion/programas", admin, new
        {
            nombre = $"PLAN {Guid.NewGuid():N}",
            fechaEmision = Hoy.ToString("yyyy-MM-dd"),
            cantidadAves = 1000,
            observaciones = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var programaId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var importar = new HttpRequestMessage(HttpMethod.Post, $"/vacunacion/programas/{programaId}/cronograma-excel")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) },
            Content = ExcelCronograma(items),
        };
        var respuesta = await cliente.SendAsync(importar);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return programaId;
    }

    [Fact]
    public async Task FlujoCompletoAdminClienteTrabajador()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"), (10, "NEWCASTLE"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();

        // Cliente: galpón con lote de 3 días y asignación del plan.
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        // Notificación: la del día 3 vence hoy; la del día 10 entra en próximas.
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/vacunacion/tareas", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, notificacion.StatusCode);
        var cuerpo = await notificacion.Content.ReadFromJsonAsync<JsonElement>();
        var vencida = cuerpo.GetProperty("vencidasYHoy").EnumerateArray().Single();
        Assert.Equal("BIO COCCIVET R", vencida.GetProperty("vacuna").GetString());
        Assert.Equal(Hoy.ToString("yyyy-MM-dd"), vencida.GetProperty("fechaProgramada").GetString());
        Assert.Equal("NEWCASTLE", cuerpo.GetProperty("proximas").EnumerateArray().Single().GetProperty("vacuna").GetString());

        // Trabajador con Vacunacion: ve la notificación y completa la tarea.
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);
        var notificacionTrabajador = await cliente.SendAsync(
            Autenticado(HttpMethod.Get, "/vacunacion/tareas", tokenTrabajador));
        Assert.Equal(HttpStatusCode.OK, notificacionTrabajador.StatusCode);
        var tareaId = vencida.GetProperty("id").GetGuid();
        var completar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/vacunacion/tareas/{tareaId}/completar", tokenTrabajador,
            new { fechaAplicacion = (string?)null, avesVacunadas = 950, observaciones = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, completar.StatusCode);

        // Segunda vez: 409 por estado (idempotencia natural, spec SP7).
        var repetir = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/vacunacion/tareas/{tareaId}/completar", tokenTrabajador, new { }));
        Assert.Equal(HttpStatusCode.Conflict, repetir.StatusCode);

        // Historial del galpón: la completada y la pendiente, con su estado.
        var historial = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareas = (await historial.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, tareas.Count);
        Assert.Equal("Completada", tareas.Single(t => t.GetProperty("id").GetGuid() == tareaId).GetProperty("estado").GetString());

        // Reasignar otro plan: la pendiente anterior se desactiva y la
        // completada queda en el historial (nada se borra físicamente).
        var otroProgramaId = await CrearProgramaConCronograma(admin, (5, "GUMBORO"));
        var reasignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId = otroProgramaId }));
        Assert.Equal(HttpStatusCode.NoContent, reasignar.StatusCode);
        var historialTras = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareasTras = (await historialTras.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, tareasTras.Count);
        Assert.Contains(tareasTras, t => t.GetProperty("vacuna").GetString() == "GUMBORO");
        Assert.Contains(tareasTras, t => t.GetProperty("estado").GetString() == "Completada");

        // Quitar el plan: desactiva las pendientes, conserva el historial.
        var quitar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/galpones/{galponId}/plan-vacunacion", tokenCliente));
        Assert.Equal(HttpStatusCode.NoContent, quitar.StatusCode);
        var historialFinal = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareasFinales = (await historialFinal.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Single(tareasFinales);
        Assert.Equal("Completada", tareasFinales[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task ClienteNoGestionaElCatalogo()
    {
        var (_, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/vacunacion/programas", tokenCliente, new
        {
            nombre = "PLAN", fechaEmision = Hoy.ToString("yyyy-MM-dd"), cantidadAves = 100, observaciones = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        var desactivar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/vacunacion/programas/{Guid.NewGuid()}", tokenCliente));
        Assert.Equal(HttpStatusCode.Forbidden, desactivar.StatusCode);
    }

    [Fact]
    public async Task AdminVeElCatalogoIncluyendoInactivosYElClienteNo()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        using var cliente = _factory.CreateClient();
        var desactivar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/vacunacion/programas/{programaId}", admin));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaAdmin = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            "/vacunacion/programas?incluirInactivos=true", admin));
        Assert.Equal(HttpStatusCode.OK, listaAdmin.StatusCode);
        Assert.Contains((await listaAdmin.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId && !p.GetProperty("estaActivo").GetBoolean());

        var (_, tokenCliente) = await CrearClienteAvicola();
        var listaCliente = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/vacunacion/programas", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, listaCliente.StatusCode);
        Assert.DoesNotContain((await listaCliente.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId);
        // Aunque pida incluirInactivos, el handler solo lo honra al Administrador.
        var listaClienteInactivos = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            "/vacunacion/programas?incluirInactivos=true", tokenCliente));
        Assert.DoesNotContain((await listaClienteInactivos.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId);
    }

    [Fact]
    public async Task TrabajadorSinVacunacionRecibe403()
    {
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["produccionhuevos"], tokenCliente);
        using var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/vacunacion/tareas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorNoPuedeAsignarNiCancelar()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId }));
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/vacunacion/tareas", tokenCliente));
        var tareaId = (await notificacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);

        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/galpones/{galponId}/plan-vacunacion", tokenTrabajador, new { programaId }));
        Assert.Equal(HttpStatusCode.Forbidden, asignar.StatusCode);
        var cancelar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/vacunacion/tareas/{tareaId}/cancelar", tokenTrabajador, new { motivo = "no corresponde" }));
        Assert.Equal(HttpStatusCode.Forbidden, cancelar.StatusCode);
    }

    [Fact]
    public async Task TareaDeOtroTenantDevuelve404()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        var (_, tokenA) = await CrearClienteAvicola();
        var (_, tokenB) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var galponId = await CrearGalpon(tokenA, Hoy.AddDays(-3));
        await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/galpones/{galponId}/plan-vacunacion", tokenA, new { programaId }));
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/vacunacion/tareas", tokenA));
        var tareaId = (await notificacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();

        var completar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/vacunacion/tareas/{tareaId}/completar", tokenB, new { }));
        Assert.Equal(HttpStatusCode.NotFound, completar.StatusCode);
        var historial = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/galpones/{galponId}/vacunacion/tareas", tokenB));
        Assert.Equal(HttpStatusCode.NotFound, historial.StatusCode);
    }

    [Fact]
    public async Task ImportacionConFilaInvalidaNoGuardaNada()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        using var cliente = _factory.CreateClient();
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/vacunacion/programas", admin, new
        {
            nombre = $"PLAN {Guid.NewGuid():N}",
            fechaEmision = Hoy.ToString("yyyy-MM-dd"),
            cantidadAves = 1000,
            observaciones = (string?)null,
        }));
        var programaId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        hoja.Cell(1, 1).Value = "FECHA";
        hoja.Cell(1, 2).Value = "EDAD";
        hoja.Cell(1, 3).Value = "VACUNA";
        hoja.Cell(2, 2).Value = "no-numero";
        hoja.Cell(2, 3).Value = "GUMBORO";
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var contenido = new MultipartFormDataContent();
        contenido.Add(new ByteArrayContent(memoria.ToArray()), "archivo", "malo.xlsx");
        var importar = new HttpRequestMessage(HttpMethod.Post, $"/vacunacion/programas/{programaId}/cronograma-excel")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) },
            Content = contenido,
        };

        var respuesta = await cliente.SendAsync(importar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problema.GetProperty("errors").GetProperty("Cronograma").GetArrayLength() > 0);
        var detalle = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/vacunacion/programas/{programaId}", admin));
        Assert.Equal(0, (await detalle.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/vacunacion/tareas");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
```

- [ ] **Step 2: Ejecutar los tests de integración dirigidos**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~VacunacionEndpointsTests"`
Expected: PASS (8 tests). Requiere Docker corriendo; si el contenedor no arranca, informar y no afirmar verde.

- [ ] **Step 3: Ejecutar la suite completa de integración y las reglas de arquitectura**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests` y `dotnet test Icarus/tests/Icarus.ArchitectureTests`
Expected: PASS ambas (incluidos los tests existentes de entitlement/sondeo, que no deben cambiar de comportamiento; las reglas de arquitectura no se modifican: ClosedXML solo entra en Infrastructure, que no tiene restricción de dependencias externas).

- [ ] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/VacunacionEndpointsTests.cs
git commit -m "test(avicola): integracion del flujo de vacunacion de punta a punta"
```

---

### Task 11: Frontend base — tipos, constantes, API y soporte FormData

**Files:**
- Modify: `web/src/lib/tipos.ts`
- Modify: `web/src/lib/http.ts:62-74`
- Modify: `web/src/features/avicola/constantes.ts`
- Modify: `web/src/features/avicola/api.ts`
- Test: `web/src/features/avicola/api.test.ts` (agregar bloque al final)

**Interfaces:**
- Consumes: DTOs del backend (Tasks 3-6): `ProgramaVacunacionResumen` → JSON `{id, nombre, fechaEmision, cantidadAves, observaciones, estaActivo}`; `ProgramaVacunacionDetalle` → lo anterior + `items: [{id, edadDia, vacuna, modoAplicacion, observaciones}]`; `TareaVacunacionResumen` → `{id, galponId, edadDia, vacuna, modoAplicacion, fechaProgramada, estado: 'Pendiente'|'Completada'|'Cancelada', fechaAplicacion, avesVacunadas, observacionesProgramadas, observacionesAplicacion, motivoCancelacion}`; `NotificacionVacunacionResumen` → `{vencidasYHoy: Tarea[], proximas: Tarea[]}`. Endpoint Excel: `POST /vacunacion/programas/{id}/cronograma-excel` multipart con campo `archivo` → `{itemsImportados}`; errores de fila llegan como 400 con `ApiError.erroresValidacion`.
- Produces (los usan las Tasks 12-14): tipos `EstadoTareaVacunacion`, `ProgramaVacunacionResumen`, `ItemPlanVacunacionResumen`, `ProgramaVacunacionDetalle`, `TareaVacunacionResumen`, `NotificacionVacunacion`; funciones `listarProgramasVacunacion`, `obtenerProgramaVacunacion`, `crearProgramaVacunacion`, `actualizarProgramaVacunacion`, `desactivarProgramaVacunacion`, `importarCronogramaExcel`, `asignarPlanVacunacion`, `quitarPlanVacunacion`, `listarTareasVacunacion`, `obtenerNotificacionVacunacion`, `completarTareaVacunacion`, `cancelarTareaVacunacion`; claves `CLAVE_PROGRAMAS_VACUNACION`, `CLAVE_NOTIFICACION_VACUNACION`, `CLAVE_TAREAS_VACUNACION`.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar al final de `web/src/features/avicola/api.test.ts` un bloque nuevo autocontenido (el archivo usa estilo compacto con helpers `r`/`solicitud`; estos tests llevan su propio stub inline para no tocar los existentes):

```ts
describe('api vacunación', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('obtenerNotificacionVacunacion hace GET /api/vacunacion/tareas', async () => {
    const cuerpo = { vencidasYHoy: [], proximas: [] };
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'content-type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    const resultado = await obtenerNotificacionVacunacion();

    expect(resultado.vencidasYHoy).toEqual([]);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.method).toBe('GET');
    expect(new URL(req.url).pathname).toBe('/api/vacunacion/tareas');
  });

  test('importarCronogramaExcel sube FormData sin Content-Type JSON', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ itemsImportados: 3 }), { status: 200, headers: { 'content-type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);
    const archivo = new File(['x'], 'plan.xlsx');

    const resultado = await importarCronogramaExcel('p1', archivo);

    expect(resultado.itemsImportados).toBe(3);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.method).toBe('POST');
    expect(new URL(req.url).pathname).toBe('/api/vacunacion/programas/p1/cronograma-excel');
    expect(req.headers.get('content-type')).not.toContain('application/json');
    expect(await req.text()).toContain('name="archivo"');
  });

  test('completarTareaVacunacion envía fecha, aves y observaciones', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await completarTareaVacunacion('t1', { fechaAplicacion: '2026-08-18', avesVacunadas: 4800, observaciones: null });

    const req = fetchMock.mock.calls[0][0] as Request;
    expect(new URL(req.url).pathname).toBe('/api/vacunacion/tareas/t1/completar');
    expect(JSON.parse(await req.clone().text())).toEqual({ fechaAplicacion: '2026-08-18', avesVacunadas: 4800, observaciones: null });
  });
});
```

Agregar el import al inicio del bloque nuevo: `import { obtenerNotificacionVacunacion, importarCronogramaExcel, completarTareaVacunacion } from './api';` (o sumarlas al import existente de `./api`).

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/api.test.ts`
Expected: FALLA la compilación TS (`obtenerNotificacionVacunacion` etc. no existen).

- [ ] **Step 3: Implementación mínima**

En `web/src/lib/tipos.ts`:

1. Cambiar la línea 12 por:

```ts
export type FuncionalidadOperativaTrabajador = 'ProduccionHuevos' | 'Mortalidad' | 'Vacunacion';
```

2. Agregar al final:

```ts
export type EstadoTareaVacunacion = 'Pendiente' | 'Completada' | 'Cancelada';

export interface ProgramaVacunacionResumen {
  id: string;
  nombre: string;
  fechaEmision: string;
  cantidadAves: number;
  observaciones: string | null;
  estaActivo: boolean;
}

export interface ItemPlanVacunacionResumen {
  id: string;
  edadDia: number;
  vacuna: string;
  modoAplicacion: string | null;
  observaciones: string | null;
}

export interface ProgramaVacunacionDetalle extends ProgramaVacunacionResumen {
  items: ItemPlanVacunacionResumen[];
}

export interface TareaVacunacionResumen {
  id: string;
  galponId: string;
  edadDia: number;
  vacuna: string;
  modoAplicacion: string | null;
  fechaProgramada: string;
  estado: EstadoTareaVacunacion;
  fechaAplicacion: string | null;
  avesVacunadas: number | null;
  observacionesProgramadas: string | null;
  observacionesAplicacion: string | null;
  motivoCancelacion: string | null;
}

export interface NotificacionVacunacion {
  vencidasYHoy: TareaVacunacionResumen[];
  proximas: TareaVacunacionResumen[];
}
```

En `web/src/lib/http.ts`, reemplazar la función `conHeaders` (líneas 62-74) por:

```ts
function conHeaders(init: RequestInit, cuerpo?: unknown): RequestInit {
  const cabeceras = new Headers(init.headers);
  cabeceras.set('X-Correlation-ID', crearCorrelationId());
  cabeceras.set('X-Session-Id', obtenerSesionId());
  const token = getAccessToken();
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);
  // FormData (subida del Excel de vacunación): el navegador fija el boundary;
  // nunca forzar Content-Type ni serializar como JSON.
  const esFormData = cuerpo instanceof FormData;
  if (cuerpo !== undefined && !esFormData) cabeceras.set('Content-Type', 'application/json');
  return {
    ...init,
    headers: cabeceras,
    body: cuerpo === undefined || esFormData ? (cuerpo as BodyInit | undefined) : JSON.stringify(cuerpo),
  };
}
```

En `web/src/features/avicola/constantes.ts`, agregar:

```ts
export const CLAVE_PROGRAMAS_VACUNACION = ['vacunacion', 'programas'] as const;
export const CLAVE_NOTIFICACION_VACUNACION = ['vacunacion', 'notificacion'] as const;
export const CLAVE_TAREAS_VACUNACION = ['vacunacion', 'tareas'] as const;
```

En `web/src/features/avicola/api.ts`, agregar al import de tipos `NotificacionVacunacion, ProgramaVacunacionDetalle, ProgramaVacunacionResumen, TareaVacunacionResumen` y al final del archivo:

```ts
export interface DatosProgramaVacunacion{nombre:string;fechaEmision:string;cantidadAves:number;observaciones:string|null}
export const listarProgramasVacunacion=(incluirInactivos=false)=>peticion<ProgramaVacunacionResumen[]>({ruta:`/vacunacion/programas${incluirInactivos?'?incluirInactivos=true':''}`});
export const obtenerProgramaVacunacion=(id:string)=>peticion<ProgramaVacunacionDetalle>({ruta:`/vacunacion/programas/${id}`});
export const crearProgramaVacunacion=(d:DatosProgramaVacunacion)=>peticion<{id:string}>({ruta:'/vacunacion/programas',metodo:'POST',cuerpo:d});
export const actualizarProgramaVacunacion=(id:string,d:DatosProgramaVacunacion)=>peticion<void>({ruta:`/vacunacion/programas/${id}`,metodo:'PUT',cuerpo:d});
export const desactivarProgramaVacunacion=(id:string)=>peticion<void>({ruta:`/vacunacion/programas/${id}`,metodo:'DELETE'});
export const importarCronogramaExcel=(id:string,archivo:File)=>{const form=new FormData();form.append('archivo',archivo);return peticion<{itemsImportados:number}>({ruta:`/vacunacion/programas/${id}/cronograma-excel`,metodo:'POST',cuerpo:form});};
export const asignarPlanVacunacion=(galponId:string,programaId:string)=>peticion<void>({ruta:`/galpones/${galponId}/plan-vacunacion`,metodo:'POST',cuerpo:{programaId}});
export const quitarPlanVacunacion=(galponId:string)=>peticion<void>({ruta:`/galpones/${galponId}/plan-vacunacion`,metodo:'DELETE'});
export const listarTareasVacunacion=(galponId:string)=>peticion<TareaVacunacionResumen[]>({ruta:`/galpones/${galponId}/vacunacion/tareas`});
export const obtenerNotificacionVacunacion=()=>peticion<NotificacionVacunacion>({ruta:'/vacunacion/tareas'});
export const completarTareaVacunacion=(id:string,d:{fechaAplicacion:string;avesVacunadas:number|null;observaciones:string|null})=>peticion<void>({ruta:`/vacunacion/tareas/${id}/completar`,metodo:'POST',cuerpo:d});
export const cancelarTareaVacunacion=(id:string,motivo:string|null)=>peticion<void>({ruta:`/vacunacion/tareas/${id}/cancelar`,metodo:'POST',cuerpo:{motivo}});
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/api.test.ts`
Expected: PASS (los tests existentes + los 3 nuevos).

- [ ] **Step 5: Commit**

```bash
git add web/src/lib/tipos.ts web/src/lib/http.ts web/src/features/avicola/constantes.ts web/src/features/avicola/api.ts web/src/features/avicola/api.test.ts
git commit -m "feat(web): api y tipos de vacunacion con soporte FormData"
```

---

### Task 12: Frontend admin — catálogo de programas con subida de Excel

**Files:**
- Create: `web/src/features/admin/vacunacion/AdminVacunacionPage.tsx`
- Test: `web/src/features/admin/vacunacion/AdminVacunacionPage.test.tsx`
- Modify: `web/src/app/paginasDiferidas.tsx`
- Modify: `web/src/app/router.tsx`
- Modify: `web/src/app/AppLayout.tsx:18`

**Interfaces:**
- Consumes (Task 11): `listarProgramasVacunacion`, `obtenerProgramaVacunacion`, `crearProgramaVacunacion`, `actualizarProgramaVacunacion`, `desactivarProgramaVacunacion`, `importarCronogramaExcel`, `DatosProgramaVacunacion`; tipos `ProgramaVacunacionResumen`, `ProgramaVacunacionDetalle`; `CLAVE_PROGRAMAS_VACUNACION`.
- Produces: ruta `/admin/vacunacion` (solo rol Administrador) y enlace "Vacunación" en el menú del Administrador.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/admin/vacunacion/AdminVacunacionPage.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AdminVacunacionPage } from './AdminVacunacionPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

function baseFetch(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(Object.entries(reglas).map(([clave, valor]) => [clave, Array.isArray(valor) ? [...valor] : [valor]]));
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    return colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><AdminVacunacionPage /></QueryClientProvider>);
}

describe('AdminVacunacionPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('lista los programas del catálogo', async () => {
    baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
    });
    renderPagina();

    expect(await screen.findByText('Plan CAISY 2026')).toBeInTheDocument();
    expect(screen.getByText(/1000 aves/)).toBeInTheDocument();
  });

  test('crea un programa con sus datos básicos', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, []),
      'POST /api/vacunacion/programas': respuesta(201, { id: 'p2' }),
    });
    renderPagina();

    await usuario.click(screen.getByRole('button', { name: 'Nuevo programa' }));
    await usuario.type(screen.getByLabelText('Nombre'), 'Plan nuevo');
    await usuario.type(screen.getByLabelText('Cantidad de aves'), '1000');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toMatchObject({ nombre: 'Plan nuevo', cantidadAves: 1000 });
  });

  test('muestra los errores por fila cuando el Excel es inválido', async () => {
    const usuario = userEvent.setup();
    baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
      'GET /api/vacunacion/programas/p1': respuesta(200, { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true, items: [] }),
      'POST /api/vacunacion/programas/p1/cronograma-excel': respuesta(400, {
        title: 'Error de validación',
        errors: { Contenido: ['Fila 4: La edad debe ser un número entero mayor que cero.'] },
      }),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Subir Excel' }));
    const input = screen.getByLabelText('Archivo Excel');
    await usuario.upload(input, new File(['x'], 'plan.xlsx'));

    expect(await screen.findByText(/Fila 4/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/admin/vacunacion/AdminVacunacionPage.test.tsx`
Expected: FALLA la compilación TS (no existe `./AdminVacunacionPage`).

- [ ] **Step 3: Implementación mínima**

`web/src/features/admin/vacunacion/AdminVacunacionPage.tsx`:

```tsx
import { Alert, Box, Button, Checkbox, Chip, CircularProgress, Container, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, List, ListItem, ListItemText, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { ApiError } from '../../../lib/http';
import type { ProgramaVacunacionResumen } from '../../../lib/tipos';
import { actualizarProgramaVacunacion, crearProgramaVacunacion, desactivarProgramaVacunacion, importarCronogramaExcel, listarProgramasVacunacion } from '../../avicola/api';
import { CLAVE_PROGRAMAS_VACUNACION } from '../../avicola/constantes';
import { hoyIso } from '../../avicola/constantes';

interface FormularioPrograma { nombre: string; fechaEmision: string; cantidadAves: string; observaciones: string; }

const formularioVacio: FormularioPrograma = { nombre: '', fechaEmision: hoyIso(), cantidadAves: '', observaciones: '' };

export function AdminVacunacionPage() {
  const queryClient = useQueryClient();
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [editando, setEditando] = useState<ProgramaVacunacionResumen | null>(null);
  const [formAbierto, setFormAbierto] = useState(false);
  const [form, setForm] = useState<FormularioPrograma>(formularioVacio);
  const [subiendoEn, setSubiendoEn] = useState<ProgramaVacunacionResumen | null>(null);
  const inputArchivo = useRef<HTMLInputElement>(null);

  const programas = useQuery({
    queryKey: [...CLAVE_PROGRAMAS_VACUNACION, incluirInactivos],
    queryFn: () => listarProgramasVacunacion(incluirInactivos),
  });

  const guardar = useMutation({
    mutationFn: () => {
      const datos = { nombre: form.nombre.trim(), fechaEmision: form.fechaEmision, cantidadAves: Number(form.cantidadAves), observaciones: form.observaciones.trim() || null };
      return editando ? actualizarProgramaVacunacion(editando.id, datos) : crearProgramaVacunacion(datos);
    },
    onSuccess: () => { setFormAbierto(false); setEditando(null); void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }); },
  });

  const desactivar = useMutation({
    mutationFn: (id: string) => desactivarProgramaVacunacion(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }),
  });

  const subirExcel = useMutation({
    mutationFn: ({ id, archivo }: { id: string; archivo: File }) => importarCronogramaExcel(id, archivo),
    onSuccess: () => { setSubiendoEn(null); void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }); },
  });

  const erroresImportacion = subirExcel.error instanceof ApiError
    ? Object.values(subirExcel.error.erroresValidacion ?? {}).flat()
    : [];

  if (programas.isLoading) return <Container sx={{ py: 3 }}><CircularProgress aria-label="Cargando" /></Container>;
  if (programas.isError) return <Container sx={{ py: 3 }}><Alert severity="error">No se pudo cargar el catálogo de vacunación.</Alert></Container>;

  return (
    <Container sx={{ py: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', my: 2 }}>
        <Typography variant="h4">Programas de vacunación</Typography>
        <Button variant="contained" onClick={() => { setEditando(null); setForm(formularioVacio); setFormAbierto(true); }}>Nuevo programa</Button>
      </Box>
      <FormControlLabel control={<Checkbox checked={incluirInactivos} onChange={(e) => setIncluirInactivos(e.target.checked)} />} label="Incluir inactivos" />
      <List aria-label="Programas de vacunación">
        {(programas.data ?? []).map((p) => (
          <ListItem key={p.id} secondaryAction={
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button size="small" onClick={() => { setEditando(p); setForm({ nombre: p.nombre, fechaEmision: p.fechaEmision, cantidadAves: String(p.cantidadAves), observaciones: p.observaciones ?? '' }); setFormAbierto(true); }}>Editar</Button>
              <Button size="small" onClick={() => { setSubiendoEn(p); inputArchivo.current?.click(); }}>Subir Excel</Button>
              {p.estaActivo && <Button size="small" color="error" onClick={() => desactivar.mutate(p.id)}>Desactivar</Button>}
            </Box>
          }>
            <ListItemText primary={<>{p.nombre} {!p.estaActivo && <Chip size="small" label="Inactivo" />}</>} secondary={`Emitido ${p.fechaEmision} · para ${p.cantidadAves} aves`} />
          </ListItem>
        ))}
      </List>
      <input ref={inputArchivo} type="file" accept=".xlsx,.xls" aria-label="Archivo Excel" style={{ display: 'none' }}
        onChange={(e) => { const archivo = e.target.files?.[0]; if (archivo && subiendoEn) subirExcel.mutate({ id: subiendoEn.id, archivo }); e.target.value = ''; }} />
      {(subirExcel.isPending || subirExcel.isError || subirExcel.isSuccess) && subiendoEn && (
        <Dialog open onClose={() => { setSubiendoEn(null); subirExcel.reset(); }}>
          <DialogTitle>Importar cronograma — {subiendoEn.nombre}</DialogTitle>
          <DialogContent>
            {subirExcel.isPending && <Typography>Subiendo…</Typography>}
            {subirExcel.isSuccess && <Alert severity="success">Cronograma importado: {subirExcel.data.itemsImportados} ítems.</Alert>}
            {subirExcel.isError && (
              <Alert severity="error">
                No se importó nada. Corregí el archivo y volvé a subirlo:
                <ul>{erroresImportacion.length > 0 ? erroresImportacion.map((m) => <li key={m}>{m}</li>) : <li>{subirExcel.error instanceof ApiError ? subirExcel.error.message : 'Error de importación.'}</li>}</ul>
              </Alert>
            )}
          </DialogContent>
          <DialogActions><Button onClick={() => { setSubiendoEn(null); subirExcel.reset(); }}>Cerrar</Button></DialogActions>
        </Dialog>
      )}
      <Dialog open={formAbierto} onClose={() => setFormAbierto(false)}>
        <DialogTitle>{editando ? 'Editar programa' : 'Nuevo programa'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          <TextField label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} fullWidth />
          <TextField label="Fecha de emisión" type="date" value={form.fechaEmision} onChange={(e) => setForm({ ...form, fechaEmision: e.target.value })} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} fullWidth />
          <TextField label="Cantidad de aves" value={form.cantidadAves} onChange={(e) => setForm({ ...form, cantidadAves: e.target.value })} inputMode="numeric" fullWidth />
          <TextField label="Observaciones" value={form.observaciones} onChange={(e) => setForm({ ...form, observaciones: e.target.value })} multiline fullWidth />
          {guardar.isError && <Alert severity="error">{guardar.error instanceof ApiError ? guardar.error.message : 'No se pudo guardar el programa.'}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFormAbierto(false)}>Cancelar</Button>
          <Button onClick={() => guardar.mutate()} disabled={guardar.isPending || !form.nombre.trim() || Number(form.cantidadAves) <= 0}>Guardar</Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
```

En `web/src/app/paginasDiferidas.tsx`, agregar al final:

```tsx
export const AdminVacunacionPage = lazy(() => import('../features/admin/vacunacion/AdminVacunacionPage').then((modulo) => ({ default: modulo.AdminVacunacionPage })));
```

En `web/src/app/router.tsx`:

1. Agregar `AdminVacunacionPage` al import de `./paginasDiferidas`.
2. Después de la ruta `/admin/clientes/:id`, agregar:

```tsx
          {
            path: '/admin/vacunacion',
            element: (
              <ProtectedRoute>
                <RequiereRol roles={admin}>
                  <AdminVacunacionPage />
                </RequiereRol>
              </ProtectedRoute>
            ),
          },
```

En `web/src/app/AppLayout.tsx` línea 18, cambiar el array del Administrador por:

```tsx
  Administrador: [{ etiqueta: 'Clientes', ruta: '/admin/clientes' }, { etiqueta: 'Vacunación', ruta: '/admin/vacunacion' }],
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/admin/vacunacion/AdminVacunacionPage.test.tsx && npm run lint`
Expected: PASS (3 tests), lint sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/admin/vacunacion web/src/app/paginasDiferidas.tsx web/src/app/router.tsx web/src/app/AppLayout.tsx
git commit -m "feat(web): catalogo admin de programas de vacunacion con subida de excel"
```

---

### Task 13: Frontend — notificación de vacunación (vencidas/hoy + próximas) con completar y cancelar

**Files:**
- Create: `web/src/features/avicola/VacunacionNotificacion.tsx`
- Create: `web/src/features/avicola/CompletarTareaDialog.tsx`
- Create: `web/src/features/avicola/CancelarTareaDialog.tsx`
- Test: `web/src/features/avicola/VacunacionNotificacion.test.tsx`
- Test: `web/src/features/avicola/CompletarTareaDialog.test.tsx`
- Modify (test): `web/src/app/inicioSegunRol.test.ts`
- Modify: `web/src/app/inicioSegunRol.ts:12`
- Modify: `web/src/app/AppLayout.tsx:27`
- Modify: `web/src/app/router.tsx:104,112,113`
- Modify: `web/src/features/avicola/GalponesPage.tsx`

**Interfaces:**
- Consumes (Task 11): `obtenerNotificacionVacunacion`, `completarTareaVacunacion`, `cancelarTareaVacunacion`; tipos `TareaVacunacionResumen`, `NotificacionVacunacion`, `Galpon`; claves `CLAVE_NOTIFICACION_VACUNACION`, `CLAVE_TAREAS_VACUNACION`.
- Produces: `VacunacionNotificacion({ galpones }: { galpones: Galpon[] })` montada en `GalponesPage`; `CompletarTareaDialog({ tarea, abierto, alCerrar }: { tarea: TareaVacunacionResumen | null; abierto: boolean; alCerrar: () => void })`; `CancelarTareaDialog` (misma firma). El trabajador con `Vacunacion` entra a `/avicola` tras el login y ve sus tareas; cancelar solo aparece al rol Cliente.

- [ ] **Step 1: Escribir los tests que fallan**

En `web/src/app/inicioSegunRol.test.ts`, después del test de producción:

```ts
  test('Trabajador con vacunación va a gestión avícola', () => {
    expect(inicioSegunRol('Trabajador', ['Vacunacion'])).toBe('/avicola');
  });
```

`web/src/features/avicola/CompletarTareaDialog.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { hoyIso } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';

const tarea: TareaVacunacionResumen = {
  id: 't1', galponId: 'ga1', edadDia: 3, vacuna: 'BIO COCCIVET R', modoAplicacion: 'Vía oral',
  fechaProgramada: hoyIso(), estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null,
  observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null,
};

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><CompletarTareaDialog tarea={tarea} abierto alCerrar={vi.fn()} /></QueryClientProvider>);
}

describe('CompletarTareaDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('la fecha de aplicación viene prellenada con hoy y se envía con las aves', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi.fn(async () => respuesta(204));
    vi.stubGlobal('fetch', fetchMock);
    renderDialog();

    expect(screen.getByLabelText('Fecha de aplicación')).toHaveValue(hoyIso());
    await usuario.type(screen.getByLabelText('Aves vacunadas'), '4800');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    expect(new URL((llamada![0] as Request).url).pathname).toBe('/api/vacunacion/tareas/t1/completar');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({ fechaAplicacion: hoyIso(), avesVacunadas: 4800, observaciones: null });
  });

  test('rechaza una fecha futura sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi.fn(async () => respuesta(204));
    vi.stubGlobal('fetch', fetchMock);
    renderDialog();

    const futura = new Date(Date.now() + 86400000 * 2);
    const iso = `${futura.getFullYear()}-${String(futura.getMonth() + 1).padStart(2, '0')}-${String(futura.getDate()).padStart(2, '0')}`;
    await usuario.clear(screen.getByLabelText('Fecha de aplicación'));
    await usuario.type(screen.getByLabelText('Fecha de aplicación'), iso);
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/no puede ser futura/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
```

`web/src/features/avicola/VacunacionNotificacion.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../auth/AuthContext';
import type { Galpon, Rol } from '../../lib/tipos';
import { hoyIso } from './constantes';
import { VacunacionNotificacion } from './VacunacionNotificacion';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

const galpones: Galpon[] = [
  { id: 'ga1', numero: '1', capacidadMaxima: 5000, gallinasActuales: 4800, fechaNacimientoLote: '2026-08-01', descripcion: null },
];

function fetchConSesion(funcionalidades: string[], reglas: Record<string, Response>, rol: Rol = 'Trabajador') {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    const fijas: Record<string, Response> = {
      'POST /api/identidad/sesion/renovar': respuesta(200, { accessToken: 't', expiraEnSegundos: 900 }),
      'GET /api/identidad/me': respuesta(200, { usuarioId: 'u1', rol, clienteId: 'cli1', trabajadorId: 'tr1', modulos: [], funcionalidades }),
    };
    return fijas[clave] ?? reglas[clave] ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderNotificacion() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><AuthProvider><VacunacionNotificacion galpones={galpones} /></AuthProvider></QueryClientProvider>);
}

describe('VacunacionNotificacion', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('con la funcionalidad muestra vencidas/hoy y próximas con el número de galpón', async () => {
    const ayer = new Date(Date.now() - 86400000);
    const isoAyer = `${ayer.getFullYear()}-${String(ayer.getMonth() + 1).padStart(2, '0')}-${String(ayer.getDate()).padStart(2, '0')}`;
    fetchConSesion(['Vacunacion'], {
      'GET /api/vacunacion/tareas': respuesta(200, {
        vencidasYHoy: [{ id: 't1', galponId: 'ga1', edadDia: 3, vacuna: 'BIO COCCIVET R', modoAplicacion: null, fechaProgramada: isoAyer, estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null, observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null }],
        proximas: [{ id: 't2', galponId: 'ga1', edadDia: 10, vacuna: 'HIPRAVIAR B1/H120', modoAplicacion: null, fechaProgramada: hoyIso(), estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null, observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null }],
      }),
    });
    renderNotificacion();

    expect(await screen.findByText(/BIO COCCIVET R/)).toBeInTheDocument();
    expect(screen.getByText(/HIPRAVIAR B1\/H120/)).toBeInTheDocument();
    expect(screen.getAllByText(/Galpón 1/).length).toBe(2);
  });

  test('sin la funcionalidad no consulta ni muestra nada', async () => {
    const fetchMock = fetchConSesion([], {});
    renderNotificacion();

    // Espera a que la sesión se resuelva; aun así no debe pedirse la notificación.
    await new Promise((resolve) => setTimeout(resolve, 50));
    const pidioTareas = fetchMock.mock.calls.some(([arg]) => new URL((arg as Request).url).pathname === '/api/vacunacion/tareas');
    expect(pidioTareas).toBe(false);
    expect(screen.queryByText(/Vacunación/)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/app/inicioSegunRol.test.ts src/features/avicola/CompletarTareaDialog.test.tsx src/features/avicola/VacunacionNotificacion.test.tsx`
Expected: `inicioSegunRol` FALLA (devuelve `/inicio`); los otros dos FALLAN la compilación TS (no existen los componentes).

- [ ] **Step 3: Implementación mínima**

`web/src/app/inicioSegunRol.ts` línea 12:

```ts
      return funcionalidades.includes('ProduccionHuevos') || funcionalidades.includes('Mortalidad') || funcionalidades.includes('Vacunacion') ? '/avicola' : '/inicio';
```

`web/src/features/avicola/CompletarTareaDialog.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '../../lib/http';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { useConexion } from '../../app/useConexion';
import { completarTareaVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION, hoyIso } from './constantes';

const esquema = z.object({
  fechaAplicacion: z.string().min(1, 'La fecha es obligatoria.').refine((f) => f <= hoyIso(), 'La fecha de aplicación no puede ser futura.'),
  avesVacunadas: z.string().refine((v) => v === '' || Number(v) > 0, 'La cantidad debe ser mayor que cero.'),
  observaciones: z.string(),
});
type DatosFormulario = z.infer<typeof esquema>;

export function CompletarTareaDialog({ tarea, abierto, alCerrar }: { tarea: TareaVacunacionResumen | null; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const { register, handleSubmit, formState: { errors } } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    defaultValues: { fechaAplicacion: hoyIso(), avesVacunadas: '', observaciones: '' },
  });
  const guardar = useMutation({
    mutationFn: (datos: DatosFormulario) => completarTareaVacunacion(tarea!.id, {
      fechaAplicacion: datos.fechaAplicacion,
      avesVacunadas: datos.avesVacunadas === '' ? null : Number(datos.avesVacunadas),
      observaciones: datos.observaciones.trim() || null,
    }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Marcar como aplicada — {tarea?.vacuna}</DialogTitle>
    <DialogContent>
      <TextField label="Fecha de aplicación" type="date" {...register('fechaAplicacion')} error={Boolean(errors.fechaAplicacion)} helperText={errors.fechaAplicacion?.message} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} fullWidth margin="dense" />
      <TextField label="Aves vacunadas" {...register('avesVacunadas')} error={Boolean(errors.avesVacunadas)} helperText={errors.avesVacunadas?.message ?? 'Dejalo vacío si se vacunó todo el galpón.'} inputMode="numeric" fullWidth margin="dense" />
      <TextField label="Observaciones" {...register('observaciones')} multiline fullWidth margin="dense" />
      {guardar.isError && <Alert severity="error" sx={{ mt: 1 }}>{guardar.error instanceof ApiError ? guardar.error.message : 'No se pudo completar la tarea.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button onClick={() => void handleSubmit((datos) => guardar.mutate(datos))()} disabled={!online || guardar.isPending || !tarea}>Guardar</Button>
    </DialogActions>
  </Dialog>;
}
```

`web/src/features/avicola/CancelarTareaDialog.tsx`:

```tsx
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '../../lib/http';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { useConexion } from '../../app/useConexion';
import { cancelarTareaVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION } from './constantes';

export function CancelarTareaDialog({ tarea, abierto, alCerrar }: { tarea: TareaVacunacionResumen | null; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const cancelar = useMutation({
    mutationFn: () => cancelarTareaVacunacion(tarea!.id, motivo.trim() || null),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      setMotivo('');
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Cancelar tarea — {tarea?.vacuna}</DialogTitle>
    <DialogContent>
      La tarea queda en el historial como cancelada y deja de aparecer en la notificación.
      <TextField label="Motivo (opcional)" value={motivo} onChange={(e) => setMotivo(e.target.value)} multiline fullWidth margin="dense" />
      {cancelar.isError && <Alert severity="error" sx={{ mt: 1 }}>{cancelar.error instanceof ApiError ? cancelar.error.message : 'No se pudo cancelar la tarea.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button color="error" onClick={() => cancelar.mutate()} disabled={!online || cancelar.isPending || !tarea}>Cancelar tarea</Button>
    </DialogActions>
  </Dialog>;
}
```

`web/src/features/avicola/VacunacionNotificacion.tsx`:

```tsx
import { Box, Button, Chip, List, ListItem, ListItemText, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { Galpon, TareaVacunacionResumen } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { obtenerNotificacionVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';
import { CancelarTareaDialog } from './CancelarTareaDialog';

export function VacunacionNotificacion({ galpones }: { galpones: Galpon[] }) {
  const puede = useFuncionalidad('Vacunacion');
  const { rol } = useAuth();
  const [completando, setCompletando] = useState<TareaVacunacionResumen | null>(null);
  const [cancelando, setCancelando] = useState<TareaVacunacionResumen | null>(null);
  const notificacion = useQuery({ queryKey: CLAVE_NOTIFICACION_VACUNACION, queryFn: obtenerNotificacionVacunacion, enabled: puede });
  if (!puede) return null;

  const numeroGalpon = (id: string) => galpones.find((g) => g.id === id)?.numero ?? '—';
  const itemTarea = (tarea: TareaVacunacionResumen) => (
    <ListItem key={tarea.id} secondaryAction={
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button size="small" variant="contained" onClick={() => setCompletando(tarea)}>Completar</Button>
        {rol === 'Cliente' && <Button size="small" color="error" onClick={() => setCancelando(tarea)}>Cancelar</Button>}
      </Box>
    }>
      <ListItemText
        primary={`Galpón ${numeroGalpon(tarea.galponId)} — ${tarea.vacuna}`}
        secondary={`Día ${tarea.edadDia} · programada ${tarea.fechaProgramada}${tarea.modoAplicacion ? ` · ${tarea.modoAplicacion}` : ''}`}
      />
    </ListItem>
  );

  const vencidasYHoy = notificacion.data?.vencidasYHoy ?? [];
  const proximas = notificacion.data?.proximas ?? [];

  return (
    <Box component="section" sx={{ my: 3 }}>
      <Typography variant="h5">Vacunación</Typography>
      {vencidasYHoy.length > 0 && (
        <>
          <Chip color="warning" size="small" label={`${vencidasYHoy.length} para hoy o vencidas`} sx={{ my: 1 }} />
          <List aria-label="Tareas de vacunación de hoy y vencidas">{vencidasYHoy.map(itemTarea)}</List>
        </>
      )}
      {proximas.length > 0 && (
        <>
          <Typography variant="h6" sx={{ mt: 2 }}>Próximas (7 días)</Typography>
          <List aria-label="Próximas vacunaciones">{proximas.map(itemTarea)}</List>
        </>
      )}
      {notificacion.data && vencidasYHoy.length === 0 && proximas.length === 0 && <Typography sx={{ mt: 1 }}>No hay vacunaciones pendientes ni próximas.</Typography>}
      <CompletarTareaDialog tarea={completando} abierto={completando !== null} alCerrar={() => setCompletando(null)} />
      <CancelarTareaDialog tarea={cancelando} abierto={cancelando !== null} alCerrar={() => setCancelando(null)} />
    </Box>
  );
}
```

En `web/src/app/AppLayout.tsx` línea 27, cambiar la condición del enlace avícola por:

```tsx
...(rol === 'Cliente' || (rol === 'Trabajador' && tieneFuncionalidad('ProduccionHuevos', 'Mortalidad', 'Vacunacion')) ? [{ etiqueta: 'Gestión Avícola', ruta: '/avicola' }] : [])
```

En `web/src/app/router.tsx`, en las tres rutas `/avicola`, `/avicola/galpones` y `/avicola/galpones/:galponId`, cambiar `funcionalidades={['ProduccionHuevos', 'Mortalidad']}` por `funcionalidades={['ProduccionHuevos', 'Mortalidad', 'Vacunacion']}` (la guarda es semántica ANY). La ruta de eficiencia queda solo con `['ProduccionHuevos']`.

En `web/src/features/avicola/GalponesPage.tsx`:

1. Agregar al import de la primera línea: `import {VacunacionNotificacion} from './VacunacionNotificacion';`
2. En el `return`, inmediatamente después de `<Container>`, insertar:

```tsx
<VacunacionNotificacion galpones={aq.data ?? []} />
```

(el componente se autogestiona: sin la funcionalidad no consulta ni renderiza nada).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/app/inicioSegunRol.test.ts src/features/avicola/CompletarTareaDialog.test.tsx src/features/avicola/VacunacionNotificacion.test.tsx && npm run lint`
Expected: PASS todos; lint sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/inicioSegunRol.ts web/src/app/inicioSegunRol.test.ts web/src/app/AppLayout.tsx web/src/app/router.tsx web/src/features/avicola/VacunacionNotificacion.tsx web/src/features/avicola/CompletarTareaDialog.tsx web/src/features/avicola/CancelarTareaDialog.tsx web/src/features/avicola/VacunacionNotificacion.test.tsx web/src/features/avicola/CompletarTareaDialog.test.tsx web/src/features/avicola/GalponesPage.tsx
git commit -m "feat(web): notificacion de vacunacion con completar y cancelar"
```

---

### Task 14: Frontend — historial del galpón y asignación de plan (cliente)

**Files:**
- Create: `web/src/features/avicola/AsignarPlanDialog.tsx`
- Test: `web/src/features/avicola/AsignarPlanDialog.test.tsx`
- Modify: `web/src/features/avicola/GalponPage.tsx`

**Interfaces:**
- Consumes (Task 11): `listarTareasVacunacion`, `listarProgramasVacunacion`, `asignarPlanVacunacion`, `quitarPlanVacunacion`; tipos `TareaVacunacionResumen`, `ProgramaVacunacionResumen`; claves `CLAVE_TAREAS_VACUNACION`, `CLAVE_PROGRAMAS_VACUNACION`, `CLAVE_NOTIFICACION_VACUNACION`. Consume `VacunacionNotificacion` (Task 13) solo como referencia de invalidación.
- Produces: sección "Vacunación" en `GalponPage` (historial con estados); `AsignarPlanDialog({ galponId, abierto, alCerrar }: { galponId: string; abierto: boolean; alCerrar: () => void })`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/AsignarPlanDialog.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AsignarPlanDialog } from './AsignarPlanDialog';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

function renderDialog(reglas: Record<string, Response>) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    return reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={queryClient}><AsignarPlanDialog galponId="ga1" abierto alCerrar={vi.fn()} /></QueryClientProvider>);
  return fn;
}

describe('AsignarPlanDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('asigna el programa elegido al galpón', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderDialog({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
      'POST /api/galpones/ga1/plan-vacunacion': respuesta(204),
    });

    await usuario.click(await screen.findByLabelText('Plan CAISY 2026'));
    await usuario.click(screen.getByRole('button', { name: 'Asignar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({ programaId: 'p1' });
  });

  test('advierte que las pendientes del plan anterior se desactivan', async () => {
    renderDialog({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
    });

    expect(await screen.findByText(/pendientes del plan anterior se desactivan/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/AsignarPlanDialog.test.tsx`
Expected: FALLA la compilación TS (no existe `./AsignarPlanDialog`).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/AsignarPlanDialog.tsx`:

```tsx
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Radio, RadioGroup, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '../../lib/http';
import { useConexion } from '../../app/useConexion';
import { asignarPlanVacunacion, listarProgramasVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_PROGRAMAS_VACUNACION, CLAVE_TAREAS_VACUNACION } from './constantes';

export function AsignarPlanDialog({ galponId, abierto, alCerrar }: { galponId: string; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const [programaId, setProgramaId] = useState('');
  const programas = useQuery({ queryKey: CLAVE_PROGRAMAS_VACUNACION, queryFn: () => listarProgramasVacunacion(), enabled: abierto });
  const asignar = useMutation({
    mutationFn: () => asignarPlanVacunacion(galponId, programaId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      setProgramaId('');
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Asignar plan de vacunación</DialogTitle>
    <DialogContent>
      {programas.isLoading && <Typography>Cargando…</Typography>}
      {programas.isError && <Alert severity="error">No se pudo cargar el catálogo.</Alert>}
      <RadioGroup value={programaId} onChange={(e) => setProgramaId(e.target.value)}>
        {(programas.data ?? []).map((p) => (
          <FormControlLabel key={p.id} value={p.id} control={<Radio />} label={p.nombre} aria-label={p.nombre} />
        ))}
      </RadioGroup>
      <Alert severity="warning" sx={{ mt: 2 }}>
        Si el galpón tiene un plan con tareas pendientes, esas pendientes se desactivan. Las completadas y canceladas se conservan como historial.
      </Alert>
      {asignar.isError && <Alert severity="error" sx={{ mt: 1 }}>{asignar.error instanceof ApiError ? asignar.error.message : 'No se pudo asignar el plan.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button variant="contained" onClick={() => asignar.mutate()} disabled={!online || asignar.isPending || !programaId}>Asignar</Button>
    </DialogActions>
  </Dialog>;
}
```

En `web/src/features/avicola/GalponPage.tsx`:

1. Agregar imports: `listarTareasVacunacion, quitarPlanVacunacion` al import de `./api`; `CLAVE_TAREAS_VACUNACION, CLAVE_NOTIFICACION_VACUNACION` al import de `./constantes`; `import { AsignarPlanDialog } from './AsignarPlanDialog';`; `import type { TareaVacunacionResumen }` sumado al import de tipos (ya importa tipos de `../../lib/tipos`).
2. Junto a los otros estados (línea ~43-47): `const [asignandoPlan, setAsignandoPlan] = useState(false);` y junto a los `useFuncionalidad` (líneas 51-52): `const puedeVacunacion = useFuncionalidad('Vacunacion');` y `const puedeEstructura = useFuncionalidad('Galpones');`.
3. Junto a las queries (líneas 53-55):

```tsx
  const tareasVacunacion = useQuery({ queryKey: [...CLAVE_TAREAS_VACUNACION, galponId], queryFn: () => listarTareasVacunacion(galponId), enabled: Boolean(galponId) && puedeVacunacion });
  const quitarPlan = useMutation({
    mutationFn: () => quitarPlanVacunacion(galponId),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION }); void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION }); },
  });
```

4. Antes del cierre `</Container>` (después del Dialog de eliminar), agregar la sección:

```tsx
    {puedeVacunacion && <Box component="section" sx={{ mt: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Vacunación</Typography>
        {puedeEstructura && <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" onClick={() => setAsignandoPlan(true)}>Asignar plan</Button>
          {(tareasVacunacion.data ?? []).some((t) => t.estado === 'Pendiente') && <Button size="small" color="error" onClick={() => quitarPlan.mutate()} disabled={quitarPlan.isPending}>Quitar plan</Button>}
        </Box>}
      </Box>
      <List aria-label="Historial de vacunación">
        {(tareasVacunacion.data ?? []).map((t: TareaVacunacionResumen) => (
          <ListItem key={t.id}>
            <ListItemText
              primary={<>{t.vacuna} <Chip size="small" label={t.estado} color={t.estado === 'Completada' ? 'success' : t.estado === 'Cancelada' ? 'default' : 'warning'} /></>}
              secondary={`Día ${t.edadDia} · programada ${t.fechaProgramada}${t.fechaAplicacion ? ` · aplicada ${t.fechaAplicacion}` : ''}${t.avesVacunadas ? ` · ${t.avesVacunadas} aves` : ''}${t.motivoCancelacion ? ` · motivo: ${t.motivoCancelacion}` : ''}`}
            />
          </ListItem>
        ))}
      </List>
      <AsignarPlanDialog galponId={galponId} abierto={asignandoPlan} alCerrar={() => setAsignandoPlan(false)} />
    </Box>}
```

5. Ajustar los estados de carga/error existentes para no romper al trabajador con solo `Vacunacion`: en la condición de carga (línea 61) y de datos (línea 71) ya se condiciona por `puedeProduccion`/`puedeMortalidad`; la query de vacunación es independiente y su error se muestra en la sección: dentro de la sección, antes del `<List>`, agregar `{tareasVacunacion.isError && <Alert severity="error">No se pudo cargar la vacunación.</Alert>}`.

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/AsignarPlanDialog.test.tsx && npm run lint && npm run build`
Expected: PASS (2 tests), lint limpio, build sin errores de tipos.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/AsignarPlanDialog.tsx web/src/features/avicola/AsignarPlanDialog.test.tsx web/src/features/avicola/GalponPage.tsx
git commit -m "feat(web): historial de vacunacion del galpon y asignacion de plan"
```

---

### Task 15: Checkbox de Vacunación en trabajadores, documentación y puerta de calidad

**Files:**
- Modify: `web/src/features/trabajadores/TrabajadoresPage.tsx:189,198,287-288`
- Modify (test): `web/src/features/trabajadores/TrabajadoresPage.test.tsx`
- Modify: `AGENTS.md` (sección Proyecto)
- Ejecutar: `node quality/generar-adaptadores.mjs` (regenera `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` y los `.*ignore`)

**Interfaces:**
- Consumes (Task 11): `FuncionalidadOperativaTrabajador` ya incluye `'Vacunacion'`.
- Produces: el cliente puede asignar `Vacunacion` a un trabajador desde la PWA; documentación del proyecto al día.

- [ ] **Step 1: Escribir el test que falla**

En `web/src/features/trabajadores/TrabajadoresPage.test.tsx`, agregar dentro del describe existente (los helpers `baseFetch`, `renderPagina`, `respuesta` y el fixture `trabajador` ya existen en el archivo):

```tsx
  test('el diálogo de funcionalidades ofrece Vacunación y la envía al guardar', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'PUT /api/clientes/cli1/trabajadores/t1/funcionalidades': respuesta(204),
    });
    renderPagina('/trabajadores');

    await usuario.click(await screen.findByRole('button', { name: 'Funcionalidades' }));
    await usuario.click(screen.getByRole('checkbox', { name: 'Vacunación' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'PUT' && req.url.endsWith('/clientes/cli1/trabajadores/t1/funcionalidades');
    });
    expect(llamada).toBeDefined();
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo.funcionalidades).toContain('Vacunacion');
  });
```

(el fixture `trabajador` del archivo tiene `funcionalidades: ['Granjas']` — bit estructural que el diálogo filtra al abrir: sirve igual para este test).

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/trabajadores/TrabajadoresPage.test.tsx`
Expected: FALLA (no existe el checkbox `Vacunación`).

- [ ] **Step 3: Implementación mínima**

En `web/src/features/trabajadores/TrabajadoresPage.tsx`:

1. Línea 189 (etiquetas en la lista): cambiar el mapa por

```tsx
{f === 'ProduccionHuevos' ? 'Producción de huevos' : f === 'Mortalidad' ? 'Mortalidad' : 'Vacunación'}
```

y el filtro de esa misma línea por `(f) => f === 'ProduccionHuevos' || f === 'Mortalidad' || f === 'Vacunacion'`.

2. Línea 198 (filtro al abrir el diálogo): agregar `|| f === 'Vacunacion'` a la condición del type guard.

3. Después de la línea 288 (checkbox Mortalidad), agregar:

```tsx
          <FormControlLabel control={<Checkbox checked={funcionalidades.includes('Vacunacion')} onChange={(e) => setFuncionalidades((actuales) => e.target.checked ? [...actuales.filter((f) => f !== 'Vacunacion'), 'Vacunacion'] : actuales.filter((f) => f !== 'Vacunacion'))} />} label="Vacunación" />
```

En `AGENTS.md`, sección Proyecto: reemplazar

```
recogidas de producción con huevos de descarte, mortalidad con
ajuste de inventario y eficiencia diaria con umbral del 70 %), con puerta de
calidad con gates de backend. El frontend React
(PWA) vive bajo `web/` e incluye la UI de Gestión Avícola online-first
(granjas, galpones, recogida, mortalidad y eficiencia).
```

por

```
recogidas de producción con huevos de descarte, mortalidad con
ajuste de inventario, eficiencia diaria con umbral del 70 % y vacunación
(catálogo global de programas de CAISY subido por el Administrador, asignación
por galpón con día 0 = fecha de poblado, notificación de tareas al trabajador)),
con puerta de calidad con gates de backend. El frontend React
(PWA) vive bajo `web/` e incluye la UI de Gestión Avícola online-first
(granjas, galpones, recogida, mortalidad, eficiencia y vacunación) y la
administración del catálogo de vacunación.
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/trabajadores/TrabajadoresPage.test.tsx && npm run lint`
Expected: PASS; lint limpio.

- [ ] **Step 5: Regenerar adaptadores**

Run: `node quality/generar-adaptadores.mjs`
Expected: regenera `CLAUDE.md`, `GEMINI.md` y `.github/copilot-instructions.md` sin errores; `git status` muestra esos archivos modificados.

- [ ] **Step 6: Puerta de calidad completa**

Run: `./verify.ps1` (Docker corriendo: los tests de integración usan Testcontainers)
Expected: todos los gates en verde. Si un gate falla, se arregla el contenido, no el gate. Prohibido `--no-verify`.

- [ ] **Step 7: Commit**

```bash
git add web/src/features/trabajadores AGENTS.md CLAUDE.md GEMINI.md .github/copilot-instructions.md .clineignore .cursorignore .geminiignore
git commit -m "feat(web): vacunacion asignable a trabajadores y docs del proyecto al dia"
```

---

## Cierre

Tras la Task 15 con la puerta en verde: `git push origin develop` (la rama de trabajo es `develop`, push directo tras verificar; `master` solo se toca a pedido explícito del usuario). Si el trabajo queda a medias, escribir `docs/ai/HANDOFF.md` desde la plantilla.
