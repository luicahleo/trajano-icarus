# Desglose visible del crédito de despachos de huevo — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Exponer los ajustes de corrección del crédito de huevo (monto, motivo, fecha) junto al saldo, mostrarlos al Cliente en los dos lugares donde ya ve el saldo, y cerrar dos brechas de rol preexistentes donde un Trabajador podía ver u operar sobre información financiera que le corresponde exclusivamente al Cliente.

**Architecture:** Backend: nuevo método de solo lectura en `IRepositorioBalanceCreditoHuevo` (`ObtenerAjustesAsync`), sin tocar `ObtenerSaldoDisponibleAsync` (camino crítico de SP9E); `ObtenerBalanceCreditoHuevoHandler` combina saldo + ajustes y exige rol Cliente; `EnviarPedidoAlimentoHandler` exige rol Cliente para confirmar un envío con crédito insuficiente; una excepción compartida (`CreditoHuevoRequiereRolClienteException`, 403) cierra ambas brechas, apoyada en un `ForbiddenException` nuevo en Building Blocks. Frontend: un componente presentacional (`AjustesCreditoHuevo`) reusado en `PedidoFormularioPage.tsx` y `PedidoAlimentoDetallePage.tsx`, que además gana el gateo por rol que hoy le falta.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs / EF Core / MediatR / xUnit + NSubstitute (unit) / Testcontainers.MsSql (integración) — backend. React + TypeScript + MUI + TanStack Query / Vitest + Testing Library — frontend (`web/`).

## Global Constraints

- Spec completa: `docs/superpowers/specs/2026-09-11-desglose-credito-huevo-design.md`. Ante cualquier duda no cubierta por este plan, esa es la fuente de verdad.
- **No modificar** `IRepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync` ni su implementación: es el camino crítico de SP9E (`EnviarPedidoAlimentoHandler`), ya probado, no se toca.
- El rol se compara contra el string literal `"Cliente"`, nunca contra `Icarus.Identity.Domain.Rol`: `Icarus.GestionAvicola.Application` no referencia `Icarus.Identity.Domain` (ver `Icarus.GestionAvicola.Application.csproj`), y `ICurrentUser.Rol` ya es `string?` por diseño para no acoplar módulos.
- `AjusteCreditoHuevoResumen.Fecha` es `DateOnly` (solo fecha, sin hora) — se deriva de `AjusteCreditoHuevo.CreadoEnUtc` (timestamp técnico) truncando la hora, nunca se expone el `DateTime` completo.
- No se exponen `IngresosDisponibles`, `RecibidoReal` ni `ComprometidoPendiente` en ningún DTO ni respuesta HTTP — decisión de alcance de la spec (Opción A), no un olvido.
- No se agrega paginación a la lista de ajustes.
- No se toca `Trajano.GestorCaisy` (ítem 3 del backlog, sesión aparte).
- TDD estricto: test primero, verlo fallar, implementar lo mínimo, verlo pasar, commitear. Cada tarea corre su **test dirigido** (no la suite completa) — la suite completa (`./verify.ps1`) corre una sola vez, en la Tarea 8, antes del cierre.
- Mensajes de commit en español, sin mojibake, UTF-8 sin BOM.
- Los tests de integración (Tarea 2 y Tarea 5) exigen Docker corriendo (Testcontainers.MsSql).

---

## Task 1: `ForbiddenException` (403) en Building Blocks

Hoy `Icarus.BuildingBlocks.Domain/DomainException.cs` no tiene ningún caso para 403: el switch de `ExceptionHandlingMiddleware` solo mapea 404/409/400/401. Esta tarea agrega la pieza reutilizable que las Tareas 3 y 4 van a lanzar.

**Files:**
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`

**Interfaces:**
- Produces: `public class ForbiddenException : DomainException` (namespace `Icarus.BuildingBlocks.Domain`), con los tres constructores estándar (`()`, `(string mensaje)`, `(string mensaje, Exception interna)`) — mismo patrón que `ConflictException`. Cualquier módulo vertical puede heredar de esta clase para obtener 403 sin acoplarse a Building Blocks (mismo mecanismo que `ConflictException`/`NotFoundException`).

- [ ] **Step 1: Escribir el test que falla**

Editar `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`: agregar este test después de `ConflictExceptionDevuelve409` (después de la línea `Assert.Equal(StatusCodes.Status409Conflict, status);` y su `}` de cierre):

```csharp
    [Fact]
    public async Task ForbiddenExceptionDevuelve403()
    {
        var (status, _) = await Ejecutar(new ForbiddenException("prohibido"));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ExceptionHandlingMiddlewareTests.ForbiddenExceptionDevuelve403"`
Expected: FALLA en compilación — `ForbiddenException` no existe todavía.

- [ ] **Step 3: Agregar `ForbiddenException` a Building Blocks**

En `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`, agregar esta clase inmediatamente después del cierre de `ConflictException` (después de su `}`) y antes de la definición de `IExcepcionConTituloPropio`:

```csharp
// 403: la sesión es válida pero el rol actual no puede realizar esta acción
// (distinto de UnauthorizedAccessException, que es sesión inválida/401).
public class ForbiddenException : DomainException
{
    public ForbiddenException() { }

    public ForbiddenException(string mensaje) : base(mensaje) { }

    public ForbiddenException(string mensaje, Exception interna) : base(mensaje, interna) { }
}
```

- [ ] **Step 4: Mapear `ForbiddenException` a 403 en el middleware**

En `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`, dentro de `EscribirProblemDetails`, el switch actual es:

```csharp
        var (status, tituloGenerico) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };
```

Cambiarlo a (agrega una línea entre `ConflictException` y `ValidationException`; debe ir antes del catch-all `DomainException` porque `ForbiddenException` hereda de `DomainException` y el pattern matching por tipo captura el primer patrón que coincide en orden de declaración):

```csharp
        var (status, tituloGenerico) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acción no permitida"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };
```

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ExceptionHandlingMiddlewareTests"`
Expected: PASA — todos los tests de esa clase en verde (el nuevo y los preexistentes).

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs
git commit -m "feat(building-blocks): agregar ForbiddenException (403) al mapeo de errores"
```

---

## Task 2: Desglose de ajustes en el repositorio de crédito

Extiende `IRepositorioBalanceCreditoHuevo` con un método de solo lectura nuevo para los ajustes, sin tocar `ObtenerSaldoDisponibleAsync`. Agrega también la excepción compartida `CreditoHuevoRequiereRolClienteException` (la van a usar las Tareas 3 y 4).

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs`

**Interfaces:**
- Consumes: `AjusteCreditoHuevo` (dominio, ya existe: `Icarus.GestionAvicola.Domain`), `GestionAvicolaDbContext.AjustesCreditoHuevo` (ya existe).
- Produces:
  - `public sealed record AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha)` (namespace `Icarus.GestionAvicola.Application.CreditoHuevo`).
  - `IRepositorioBalanceCreditoHuevo.ObtenerAjustesAsync(Guid clienteId, CancellationToken cancellationToken = default)` → `Task<IReadOnlyList<AjusteCreditoHuevoResumen>>`, ordenados por `CreadoEnUtc` descendente.
  - `public sealed class CreditoHuevoRequiereRolClienteException(string mensaje) : ForbiddenException(mensaje)` (namespace `Icarus.GestionAvicola.Application.CreditoHuevo`) — la consumen las Tareas 3 y 4.

- [ ] **Step 1: Escribir el test de integración que falla**

`Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs` es el archivo de pruebas de este repositorio: ya tiene los helpers `SaldoDeAsync`/`SembrarAsync`, el patrón de sembrar un tenant propio por prueba, y ya cubre ajustes en el saldo (`UnAjusteDeCreditoSumaAlSaldoSinDesfase`).

Agregar un helper paralelo a `SaldoDeAsync`, justo después de él (después de su `}` de cierre, antes de `SembrarAsync`):

```csharp
    private async Task<IReadOnlyList<AjusteCreditoHuevoResumen>> AjustesDeAsync(Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        return await repositorio.ObtenerAjustesAsync(clienteId);
    }
```

Y agregar este `[Fact]` al final de la clase, antes del `}` que cierra `BalanceCreditoHuevoTests` (después de `UnAjusteDeCreditoSumaAlSaldoSinDesfase`):

```csharp

    // Desglose visible del crédito (spec, ítem 2 del backlog): la lista de
    // ajustes es un método aparte de ObtenerSaldoDisponibleAsync — el saldo
    // sigue siendo un solo número y no se toca.
    [Fact]
    public async Task ObtenerAjustesDevuelveSoloLosDelClienteOrdenadosPorFechaDescendente()
    {
        var clienteId = Guid.NewGuid();
        var otroClienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // Dos SembrarAsync separados: CreadoEnUtc se fija en el constructor
        // del agregado (DateTime.UtcNow) y el round-trip a SQL entre ambas
        // siembras garantiza un timestamp estrictamente mayor para el
        // segundo, sin depender de la resolución del reloj del proceso.
        var masAntiguo = new AjusteCreditoHuevo(
            clienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            45m, "Corrección de precio Extra.", actorId);
        await SembrarAsync(masAntiguo);

        var masReciente = new AjusteCreditoHuevo(
            clienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            -10m, "Corrección de precio Primera.", actorId);
        var deOtroCliente = new AjusteCreditoHuevo(
            otroClienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            5m, "Ajeno.", actorId);
        await SembrarAsync(masReciente, deOtroCliente);

        var ajustes = await AjustesDeAsync(clienteId);

        Assert.Equal(2, ajustes.Count);
        Assert.Equal(masReciente.Id, ajustes[0].Id);
        Assert.Equal(-10m, ajustes[0].Monto);
        Assert.Equal("Corrección de precio Primera.", ajustes[0].Motivo);
        Assert.Equal(masAntiguo.Id, ajustes[1].Id);
        Assert.DoesNotContain(ajustes, a => a.Id == deOtroCliente.Id);
    }
```

No hace falta agregar ningún `using`: el archivo ya importa `Icarus.GestionAvicola.Application.CreditoHuevo` (donde viven `IRepositorioBalanceCreditoHuevo` y el nuevo `AjusteCreditoHuevoResumen`), `Icarus.GestionAvicola.Domain` (`AjusteCreditoHuevo`) y `Microsoft.Extensions.DependencyInjection`.

- [ ] **Step 2: Correr el test y verificar que falla**

Requiere Docker corriendo.
Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~BalanceCreditoHuevoTests.ObtenerAjustesDevuelveSoloLosDelClienteOrdenadosPorFechaDescendente"`
Expected: FALLA en compilación — `ObtenerAjustesAsync` y `AjusteCreditoHuevoResumen` no existen todavía.

- [ ] **Step 3: Extender el puerto (`PuertoCreditoHuevo.cs`)**

El archivo completo hoy es:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Crédito por huevos despachados (spec SP9): disponible recién catorce días
// después de la recepción. Sin tabla de saldo persistida: se calcula por
// consulta (suma de ingresos disponibles menos egresos reales).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);
}

public static class ReglasCreditoHuevo
{
    public const int DiasDisponibilidadCredito = 14;
}

// Exige confirmación explícita del cliente antes de enviar un pedido que
// dejaría su crédito por despachos de huevo en negativo (spec SP9E). Hereda
// de ConflictException (409): la decisión final de aceptar el pedido sigue
// siendo de CAISY al aceptar/rechazar, así que esto no es un bloqueo duro
// sin salida — solo exige el paso consciente de confirmar.
public sealed class CreditoInsuficienteRequiereConfirmacionException
    : ConflictException, IExcepcionConTituloPropio
{
    private const string MensajePorDefecto =
        "Este pedido dejaría el crédito del cliente en negativo. " +
        "Confirmá el envío para continuar.";

    public CreditoInsuficienteRequiereConfirmacionException() : base(MensajePorDefecto) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje) : base(mensaje) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje, Exception interna)
        : base(mensaje, interna) { }

    public string Titulo => "Crédito insuficiente";
}
```

Reemplazar el contenido completo del archivo por:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Crédito por huevos despachados (spec SP9): disponible recién catorce días
// después de la recepción. Sin tabla de saldo persistida: se calcula por
// consulta (suma de ingresos disponibles menos egresos reales).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    // Desglose visible del crédito: solo los ajustes de corrección, el único
    // componente que representa una sorpresa real para el cliente (spec,
    // ítem 2 del backlog). No toca ObtenerSaldoDisponibleAsync, el camino
    // crítico de SP9E.
    Task<IReadOnlyList<AjusteCreditoHuevoResumen>> ObtenerAjustesAsync(
        Guid clienteId, CancellationToken cancellationToken = default);
}

public static class ReglasCreditoHuevo
{
    public const int DiasDisponibilidadCredito = 14;
}

// Exige confirmación explícita del cliente antes de enviar un pedido que
// dejaría su crédito por despachos de huevo en negativo (spec SP9E). Hereda
// de ConflictException (409): la decisión final de aceptar el pedido sigue
// siendo de CAISY al aceptar/rechazar, así que esto no es un bloqueo duro
// sin salida — solo exige el paso consciente de confirmar.
public sealed class CreditoInsuficienteRequiereConfirmacionException
    : ConflictException, IExcepcionConTituloPropio
{
    private const string MensajePorDefecto =
        "Este pedido dejaría el crédito del cliente en negativo. " +
        "Confirmá el envío para continuar.";

    public CreditoInsuficienteRequiereConfirmacionException() : base(MensajePorDefecto) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje) : base(mensaje) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje, Exception interna)
        : base(mensaje, interna) { }

    public string Titulo => "Crédito insuficiente";
}

// Regla de negocio: el crédito de huevo (saldo, ajustes y confirmación de
// envío con crédito insuficiente) es exclusivo del Cliente, nunca del
// Trabajador — aunque tenga el entitlement de módulo (spec, ítem 2 del
// backlog). Un solo tipo para los dos puntos que la disparan:
// ObtenerBalanceCreditoHuevoHandler y EnviarPedidoAlimentoHandler.
public sealed class CreditoHuevoRequiereRolClienteException(string mensaje)
    : ForbiddenException(mensaje);
```

- [ ] **Step 4: Agregar `AjusteCreditoHuevoResumen` y extender `BalanceCreditoHuevoResumen` (`ComandosCreditoHuevo.cs`)**

El archivo completo hoy es:

```csharp
using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

public sealed record ObtenerBalanceCreditoHuevoQuery : IRequest<BalanceCreditoHuevoResumen>;

public sealed record BalanceCreditoHuevoResumen(decimal SaldoDisponible);

public sealed class ObtenerBalanceCreditoHuevoHandler(
    IRepositorioBalanceCreditoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ObtenerBalanceCreditoHuevoQuery, BalanceCreditoHuevoResumen>
{
    public async Task<BalanceCreditoHuevoResumen> Handle(
        ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(
            clienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        return new BalanceCreditoHuevoResumen(saldo);
    }
}
```

Esta tarea solo toca el DTO (la Tarea 3 toca el `Handler`). Reemplazar el contenido completo del archivo por:

```csharp
using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

public sealed record ObtenerBalanceCreditoHuevoQuery : IRequest<BalanceCreditoHuevoResumen>;

public sealed record AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

public sealed record BalanceCreditoHuevoResumen(
    decimal SaldoDisponible, IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);

public sealed class ObtenerBalanceCreditoHuevoHandler(
    IRepositorioBalanceCreditoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ObtenerBalanceCreditoHuevoQuery, BalanceCreditoHuevoResumen>
{
    public async Task<BalanceCreditoHuevoResumen> Handle(
        ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(
            clienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        return new BalanceCreditoHuevoResumen(saldo, []);
    }
}
```

Nota: el `Handle` de arriba todavía **no** llama a `ObtenerAjustesAsync` ni chequea rol — eso es exactamente lo que hace la Tarea 3. Esta tarea solo deja el build compilando con la nueva forma del DTO (`Ajustes` en `[]` vacío) para poder implementar el repositorio de forma aislada.

- [ ] **Step 5: Implementar `ObtenerAjustesAsync` (`RepositorioBalanceCreditoHuevo.cs`)**

El final del archivo hoy (el cierre de `ObtenerSaldoDisponibleAsync` y el cierre de la clase) es exactamente:

```csharp
        return ingresos - recibidoReal - comprometidoPendiente + ajustes;
    }
}
```

Reemplazar esas tres líneas por (agrega el método nuevo entre el cierre de `ObtenerSaldoDisponibleAsync` y el cierre de la clase, sin tocar una sola línea del cuerpo de `ObtenerSaldoDisponibleAsync` que queda arriba):

```csharp
        return ingresos - recibidoReal - comprometidoPendiente + ajustes;
    }

    // Materializa las filas (columnas simples, sin cómputo) y recién después
    // convierte CreadoEnUtc a DateOnly en memoria: DateOnly.FromDateTime
    // dentro de un Select traducido a SQL es una fuente de errores de
    // traducción de EF Core en otras partes de este mismo archivo (ver
    // comentario de clase), así que se evita a propósito.
    public async Task<IReadOnlyList<AjusteCreditoHuevoResumen>> ObtenerAjustesAsync(
        Guid clienteId, CancellationToken cancellationToken = default)
    {
        var ajustes = await db.AjustesCreditoHuevo
            .Where(a => a.ClienteId == clienteId)
            .OrderByDescending(a => a.CreadoEnUtc)
            .ToListAsync(cancellationToken);
        return ajustes
            .Select(a => new AjusteCreditoHuevoResumen(
                a.Id, a.Monto, a.Motivo, DateOnly.FromDateTime(a.CreadoEnUtc)))
            .ToList();
    }
}
```

- [ ] **Step 6: Correr el test y verificar que pasa**

Requiere Docker corriendo.
Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~BalanceCreditoHuevoTests"`
Expected: PASA — el test nuevo y los 5 preexistentes de esa clase en verde (los preexistentes ejercitan `ObtenerSaldoDisponibleAsync`, que no se tocó: si alguno se pone rojo, se rompió el camino crítico de SP9E y hay que revertir).

- [ ] **Step 7: Build completo del backend**

Run: `dotnet build Icarus/Icarus.sln`
Expected: 0 errores (confirma que ningún otro archivo quedó roto por el cambio de forma de `BalanceCreditoHuevoResumen`).

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs
git commit -m "feat(avicola): exponer los ajustes de credito de huevo desde el repositorio"
```

---

## Task 3: `ObtenerBalanceCreditoHuevoHandler` combina saldo + ajustes y exige rol Cliente

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerBalanceCreditoHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioBalanceCreditoHuevo` (Tarea 2), `AjusteCreditoHuevoResumen` (Tarea 2), `CreditoHuevoRequiereRolClienteException` (Tarea 2), `ICurrentUser` (`Icarus.BuildingBlocks.Application`, ya existe: `ClienteId`, `Rol`).
- Produces: `ObtenerBalanceCreditoHuevoHandler.Handle(...)` devuelve `BalanceCreditoHuevoResumen` con `Ajustes` real (ya no `[]` fijo) y lanza `CreditoHuevoRequiereRolClienteException` si `usuarioActual.Rol != "Cliente"`.

**`DespachosHuevoEndpoints.cs` NO se toca en esta tarea ni en ninguna otra del plan.** El endpoint `GET /despachos-huevo/credito` conserva su ruta, su método y su `RequireAuthorization(PoliticasClientes.Para(Funcionalidades.DespachoHuevo))`. El chequeo de rol va deliberadamente dentro del handler y no en la política: `PoliticasClientes.Para(...)` es genérica por funcionalidad y la comparten muchos otros endpoints, así que meterle la distinción Cliente/Trabajador los afectaría a todos. Lo único que cambia del endpoint es la forma del JSON que devuelve, y eso sale del DTO.

- [ ] **Step 1: Escribir el archivo de test que falla**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerBalanceCreditoHuevoHandlerTests.cs` con este contenido completo:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// Desglose visible del crédito (spec, ítem 2 del backlog): el handler
// combina el saldo (sin tocar) con los ajustes nuevos, y exige rol Cliente
// — la misma regla que EnviarPedidoAlimentoHandler exige para confirmar un
// envío con crédito insuficiente (ver PedidosAlimentoHandlerTests.cs).
public class ObtenerBalanceCreditoHuevoHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioBalanceCreditoHuevo _repositorio =
        Substitute.For<IRepositorioBalanceCreditoHuevo>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();

    public ObtenerBalanceCreditoHuevoHandlerTests()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _usuarioActual.Rol.Returns("Cliente");
    }

    private ObtenerBalanceCreditoHuevoHandler CrearHandler() => new(_repositorio, _usuarioActual);

    [Fact]
    public async Task CombinaSaldoYAjustesDelRepositorio()
    {
        _repositorio.ObtenerSaldoDisponibleAsync(ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(1250m);
        var ajustes = new List<AjusteCreditoHuevoResumen>
        {
            new(Guid.NewGuid(), 45m, "Corrección de precio Extra.", new DateOnly(2026, 9, 1)),
        };
        _repositorio.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>()).Returns(ajustes);

        var resultado = await CrearHandler().Handle(
            new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None);

        Assert.Equal(1250m, resultado.SaldoDisponible);
        Assert.Same(ajustes, resultado.Ajustes);
    }

    [Fact]
    public async Task SinAjustesDevuelveListaVacia()
    {
        _repositorio.ObtenerSaldoDisponibleAsync(ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(0m);
        _repositorio.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>())
            .Returns(new List<AjusteCreditoHuevoResumen>());

        var resultado = await CrearHandler().Handle(
            new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None);

        Assert.Empty(resultado.Ajustes);
    }

    [Fact]
    public async Task SinCuentaDeTenantFalla()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task UnTrabajadorNoPuedeConsultarElCredito()
    {
        _usuarioActual.Rol.Returns("Trabajador");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearHandler().Handle(new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None));

        await _repositorio.DidNotReceive().ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _repositorio.DidNotReceive().ObtenerAjustesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerBalanceCreditoHuevoHandlerTests"`
Expected: FALLA — `CombinaSaldoYAjustesDelRepositorio` y `SinAjustesDevuelveListaVacia` fallan porque el handler todavía devuelve `Ajustes` fijo en `[]`; `UnTrabajadorNoPuedeConsultarElCredito` falla porque no se lanza ninguna excepción todavía.

- [ ] **Step 3: Implementar el `Handle` completo**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs`, reemplazar el método `Handle` (el que quedó con `return new BalanceCreditoHuevoResumen(saldo, []);` al final de la Tarea 2) por:

```csharp
    public async Task<BalanceCreditoHuevoResumen> Handle(
        ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        if (usuarioActual.Rol != "Cliente")
            throw new CreditoHuevoRequiereRolClienteException(
                "El crédito por despachos de huevo es exclusivo del Cliente.");
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(
            clienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        var ajustes = await repositorio.ObtenerAjustesAsync(clienteId, cancellationToken);
        return new BalanceCreditoHuevoResumen(saldo, ajustes);
    }
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerBalanceCreditoHuevoHandlerTests"`
Expected: PASA — los 4 tests en verde.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerBalanceCreditoHuevoHandlerTests.cs
git commit -m "feat(avicola): ObtenerBalanceCreditoHuevoHandler combina ajustes y exige rol Cliente"
```

---

## Task 4: `EnviarPedidoAlimentoHandler` exige rol Cliente para confirmar crédito insuficiente

Cierra la brecha de rol en el flujo de SP9E: hoy cualquier rol con el entitlement `PedidoAlimento` puede confirmar un envío con crédito insuficiente.

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`

**Interfaces:**
- Consumes: `CreditoHuevoRequiereRolClienteException` (Tarea 2), `ICurrentUser.Rol` (ya inyectado en `EnviarPedidoAlimentoHandler` como `usuarioActual`).
- Produces: `EnviarPedidoAlimentoHandler.Handle(...)` lanza `CreditoHuevoRequiereRolClienteException` cuando `request.ConfirmarCreditoInsuficiente` es `true`, el saldo resultante es negativo, y `usuarioActual.Rol != "Cliente"`.

- [ ] **Step 1: Fijar el rol por defecto en el fixture de test (antes de escribir el test nuevo)**

En `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`, el constructor hoy es:

```csharp
    public PedidosAlimentoHandlerTests()
    {
        _usuarioActual.EstaAutenticado.Returns(true);
        _usuarioActual.UsuarioId.Returns(UsuarioId);
        _usuarioActual.ClienteId.Returns(ClienteId);
        _repositorio.IniciarTransaccionAsync(Arg.Any<CancellationToken>())
            .Returns(_transaccion);
        // Saldo suficiente por defecto: los tests que no versan sobre crédito
        // (cupo semanal, congelado de precios, etc.) no deben verse afectados
        // por SP9E. Los tests de crédito lo sobreescriben explícitamente.
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1_000_000m);
    }
```

**Motivo de este paso** (hacerlo primero, no al final): el fixture nunca configura `_usuarioActual.Rol` hoy, así que NSubstitute devuelve `null` por defecto. El test ya existente `EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial` confirma un envío con `ConfirmarCreditoInsuficiente: true` y hoy pasa — si se agrega el chequeo de rol sin fijar este default, ese test empezaría a fallar porque `null != "Cliente"`. Fijar `"Cliente"` como default preserva a todos los tests existentes y dedica solo el test nuevo (Step 3) a sobreescribirlo con `"Trabajador"`.

Cambiar el constructor a:

```csharp
    public PedidosAlimentoHandlerTests()
    {
        _usuarioActual.EstaAutenticado.Returns(true);
        _usuarioActual.UsuarioId.Returns(UsuarioId);
        _usuarioActual.ClienteId.Returns(ClienteId);
        _usuarioActual.Rol.Returns("Cliente");
        _repositorio.IniciarTransaccionAsync(Arg.Any<CancellationToken>())
            .Returns(_transaccion);
        // Saldo suficiente por defecto: los tests que no versan sobre crédito
        // (cupo semanal, congelado de precios, etc.) no deben verse afectados
        // por SP9E. Los tests de crédito lo sobreescriben explícitamente.
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1_000_000m);
    }
```

- [ ] **Step 2: Correr la suite de este archivo y verificar que sigue en verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"`
Expected: PASA — el cambio del Step 1 es un no-op de comportamiento (el rol nunca se leía todavía), confirma que no se rompió nada antes de seguir.

- [ ] **Step 3: Escribir el test nuevo que falla**

Agregar este `[Fact]` en el mismo archivo, después de `EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial` (después de su `}` de cierre):

```csharp
    // Segunda brecha de rol cerrada por el mismo ítem (spec, backlog #2): la
    // política del endpoint es por entitlement de módulo, no por rol, así
    // que sin este chequeo un Trabajador con PedidoAlimento podría confirmar
    // un envío que deja el crédito del cliente en negativo.
    [Fact]
    public async Task UnTrabajadorNoPuedeConfirmarElEnvioConCreditoInsuficiente()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);
        _usuarioActual.Rol.Returns("Trabajador");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id, ConfirmarCreditoInsuficiente: true),
                CancellationToken.None));

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Empty(pedido.Historial);
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaccion.DidNotReceive().ConfirmarAsync(Arg.Any<CancellationToken>());
    }
```

- [ ] **Step 4: Correr el test y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~UnTrabajadorNoPuedeConfirmarElEnvioConCreditoInsuficiente"`
Expected: FALLA — hoy nada lanza `CreditoHuevoRequiereRolClienteException`, el envío se completa igual.

- [ ] **Step 5: Agregar el chequeo de rol en `EnviarPedidoAlimentoHandler`**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`, el bloque actual (dentro de `EnviarPedidoAlimentoHandler.Handle`) es:

```csharp
            haySaldoInsuficiente = saldoResultante < 0;
            if (haySaldoInsuficiente)
            {
                if (!request.ConfirmarCreditoInsuficiente)
                    throw new CreditoInsuficienteRequiereConfirmacionException();
                motivoCreditoInsuficiente = string.Create(CultureInfo.InvariantCulture,
                    $"Enviado con crédito insuficiente: saldo {saldoActual}, pedido {totalEsperado}, resultante {saldoResultante}.");
            }
```

Cambiarlo a (agrega el chequeo de rol entre el primer `throw` y el armado del motivo):

```csharp
            haySaldoInsuficiente = saldoResultante < 0;
            if (haySaldoInsuficiente)
            {
                if (!request.ConfirmarCreditoInsuficiente)
                    throw new CreditoInsuficienteRequiereConfirmacionException();
                if (usuarioActual.Rol != "Cliente")
                    throw new CreditoHuevoRequiereRolClienteException(
                        "Solo el Cliente puede confirmar el envío con crédito insuficiente.");
                motivoCreditoInsuficiente = string.Create(CultureInfo.InvariantCulture,
                    $"Enviado con crédito insuficiente: saldo {saldoActual}, pedido {totalEsperado}, resultante {saldoResultante}.");
            }
```

`usuarioActual` ya es un parámetro del constructor de `EnviarPedidoAlimentoHandler`; `CreditoHuevoRequiereRolClienteException` ya es accesible porque el archivo ya tiene `using Icarus.GestionAvicola.Application.CreditoHuevo;` en su cabecera.

- [ ] **Step 6: Correr los tests y verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"`
Expected: PASA — toda la clase en verde, incluyendo el test nuevo y `EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial` (que sigue pasando porque el fixture ahora declara rol Cliente por defecto).

- [ ] **Step 7: Verificar el otro archivo que monta este mismo handler**

`Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesInternasTests.cs` también construye `EnviarPedidoAlimentoHandler` (helper `CrearEnviador(ICurrentUser)`) y su `_usuarioTenant` tampoco declara `Rol`. **No** necesita cambios: su constructor fija el saldo por defecto en `1_000_000m` y ningún test de esa clase lo baja, así que `haySaldoInsuficiente` siempre es `false` y el chequeo de rol nuevo nunca se alcanza. Este paso solo confirma ese razonamiento con el test runner, en vez de asumirlo.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~NotificacionesInternasTests"`
Expected: PASA — toda la clase en verde, sin haber tocado el archivo. Si alguno falla con `CreditoHuevoRequiereRolClienteException`, agregar `_usuarioTenant.Rol.Returns("Cliente");` al constructor de esa clase (junto a las otras líneas de `_usuarioTenant`) y volver a correr.

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs
git commit -m "feat(avicola): solo el Cliente puede confirmar un envio con credito insuficiente"
```

---

## Task 5: Tests de integración de los dos gates de rol + ajustes en el endpoint

**Files:**
- Modify: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`
- Modify: `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs`

**Interfaces:**
- Consumes: endpoints ya existentes (`POST /pedidos-alimento/{id}/enviar`, `GET /despachos-huevo/credito`) con el comportamiento de las Tareas 2-4; endpoints de `Clientes` ya existentes (`POST /clientes/{clienteId}/trabajadores`, `PUT /clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades`).
- Produces: ninguna API nueva — solo cobertura de los dos 403 y de `ajustes` en la respuesta HTTP.

Requiere Docker corriendo (Testcontainers.MsSql).

### Parte A — `PedidosAlimentoEndpointsTests.cs`: Trabajador no puede confirmar el envío

- [ ] **Step 1: Escribir el test**

En `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`, el helper `CrearClienteConGestionAvicolaAsync` hoy es:

```csharp
    private async Task<(HttpClient Cliente, string Token)> CrearClienteConGestionAvicolaAsync()
    {
        var cliente = _factory.CreateClient();
        var tokenAdmin = await LoginComo(cliente, SemillaIdentidad.EmailAdmin);
        var email = $"pedidos-cliente-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", tokenAdmin,
            JsonContent.Create(new
            {
                razonSocial = "Granja de Prueba S.A.C.",
                identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
                email,
                contrasena = "Clave-Cliente-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var clienteId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", tokenAdmin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (cliente, await LoginComo(cliente, email, "Clave-Cliente-123"));
    }
```

Reemplazarlo por (agrega `clienteId` a la tupla devuelta, necesario para dar de alta un trabajador bajo ese mismo tenant):

```csharp
    private async Task<(HttpClient Cliente, string Token, Guid ClienteId)> CrearClienteConGestionAvicolaAsync()
    {
        var cliente = _factory.CreateClient();
        var tokenAdmin = await LoginComo(cliente, SemillaIdentidad.EmailAdmin);
        var email = $"pedidos-cliente-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", tokenAdmin,
            JsonContent.Create(new
            {
                razonSocial = "Granja de Prueba S.A.C.",
                identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
                email,
                contrasena = "Clave-Cliente-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var clienteId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", tokenAdmin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (cliente, await LoginComo(cliente, email, "Clave-Cliente-123"), clienteId);
    }

    // Trabajador nuevo bajo el tenant recién creado, con solo la
    // funcionalidad indicada (spec, ítem 2 del backlog: el Trabajador tiene
    // el entitlement de módulo pero no debe poder confirmar un envío con
    // crédito insuficiente).
    private static async Task<string> CrearTrabajadorConFuncionAsync(
        HttpClient cliente, string tokenCliente, Guid clienteId, string funcionalidad)
    {
        var email = $"pedidos-trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post,
            $"/api/clientes/{clienteId}/trabajadores", tokenCliente,
            JsonContent.Create(new
            {
                nombre = "Trabajador de Prueba",
                documentoIdentidad = $"9{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Operario",
                fechaIngreso = "2026-01-15",
                email,
                contrasena = "Clave-Trabajador-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = await cliente.SendAsync(Pedido(HttpMethod.Put,
            $"/api/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            JsonContent.Create(new { funcionalidades = new[] { funcionalidad } })));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        return await LoginComo(cliente, email, "Clave-Trabajador-123");
    }
```

Actualizar el único call site existente (dentro de `EnviarSinConfirmarConCreditoInsuficienteExigeConfirmacionYElReintentoLoAcepta`), que hoy dice:

```csharp
        var (cliente, tokenCliente) = await CrearClienteConGestionAvicolaAsync();
```

Cambiarlo a:

```csharp
        var (cliente, tokenCliente, _) = await CrearClienteConGestionAvicolaAsync();
```

Agregar el test nuevo al final de la clase, antes del último `}` que cierra `PedidosAlimentoEndpointsTests` (después de `EnviarSinConfirmarConCreditoInsuficienteExigeConfirmacionYElReintentoLoAcepta`):

```csharp

    // Segunda brecha de rol cerrada por el mismo ítem del backlog: aunque el
    // Trabajador tenga la funcionalidad PedidoAlimento (entitlement de
    // módulo), no puede confirmar un envío con crédito insuficiente —
    // llamando la API directo, saltando la UI que ya no le ofrece ese botón.
    [Fact]
    public async Task UnTrabajadorNoPuedeConfirmarElEnvioConCreditoInsuficiente()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);
        var pedidoId = await CrearBorradorAsync(cliente, tokenCliente);
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "PedidoAlimento");

        var intento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{pedidoId}/enviar", tokenTrabajador,
            JsonContent.Create(new { confirmarCreditoInsuficiente = true })));

        Assert.Equal(HttpStatusCode.Forbidden, intento.StatusCode);
        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, pedidoId);
        Assert.Equal("Borrador", detalle.GetProperty("estado").GetString());
        Assert.Equal(0, detalle.GetProperty("historial").GetArrayLength());
    }
```

- [ ] **Step 2: Correr el test y confirmar que pasa de punta a punta**

Requiere Docker corriendo. Este test es de confirmación, no de TDD rojo/verde: el 403 que verifica ya lo implementó la Tarea 4 a nivel unitario (`PedidosAlimentoHandlerTests.cs`), así que acá no se espera que falle — su valor es probar el camino completo por HTTP (autorización de ASP.NET, entitlement del trabajador, el nuevo `ForbiddenException`) y quedar como regresión si algo de esa cadena se rompe más adelante.
Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidosAlimentoEndpointsTests"`
Expected: PASA — toda la clase en verde (el test nuevo y los preexistentes, incluido el que usa el helper con su nueva forma de tupla). Si el test nuevo da 204 en vez de 403, revisar que las Tareas 1-4 estén aplicadas antes de continuar.

### Parte B — `DespachosHuevoCaisyEndpointsTests.cs`: Trabajador no puede consultar el crédito, ajustes en la respuesta

- [ ] **Step 3: Escribir los tests**

Igual que en la Parte A, estos dos tests son de confirmación de punta a punta, no de TDD rojo/verde: el 403 y los `ajustes` en la respuesta ya los implementan las Tareas 2 y 3 a nivel unitario/repositorio. Su valor es probar el camino completo por HTTP.

En `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs`, agregar a los `using` del principio del archivo (la lista actual es `System.Net`, `System.Net.Http.Headers`, `System.Net.Http.Json`, `System.Text.Json`, `ClosedXML.Excel`, `Icarus.Identity.Infrastructure`, `SkiaSharp`, `Xunit`):

```csharp
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;
```

Agregar estos dos helpers privados dentro de la clase (junto a los demás helpers, por ejemplo después de `CrearCuentaCaisyAsync`):

```csharp
    // Tenant nuevo con el módulo GestionAvicola (habilita DespachoHuevo para
    // el Cliente): mismo patrón que PedidosAlimentoEndpointsTests.cs,
    // duplicado a propósito para mantener cada archivo de pruebas
    // autocontenido (convención ya usada en el resto de esta clase).
    private async Task<(HttpClient Cliente, string Token, Guid ClienteId)> CrearClienteConGestionAvicolaAsync()
    {
        var cliente = _factory.CreateClient();
        var tokenAdmin = await LoginComo(cliente, SemillaIdentidad.EmailAdmin);
        var email = $"despachos-credito-cliente-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", tokenAdmin,
            JsonContent.Create(new
            {
                razonSocial = "Granja de Prueba S.A.C.",
                identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
                email,
                contrasena = "Clave-Cliente-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var clienteId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", tokenAdmin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (cliente, await LoginComo(cliente, email, "Clave-Cliente-123"), clienteId);
    }

    private static async Task<string> CrearTrabajadorConFuncionAsync(
        HttpClient cliente, string tokenCliente, Guid clienteId, string funcionalidad)
    {
        var email = $"despachos-credito-trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post,
            $"/api/clientes/{clienteId}/trabajadores", tokenCliente,
            JsonContent.Create(new
            {
                nombre = "Trabajador de Prueba",
                documentoIdentidad = $"9{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Operario",
                fechaIngreso = "2026-01-15",
                email,
                contrasena = "Clave-Trabajador-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = await cliente.SendAsync(Pedido(HttpMethod.Put,
            $"/api/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            JsonContent.Create(new { funcionalidades = new[] { funcionalidad } })));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        return await LoginComo(cliente, email, "Clave-Trabajador-123");
    }
```

Agregar estos dos tests al final de la clase, antes del último `}` que cierra `DespachosHuevoCaisyEndpointsTests`:

```csharp

    // Segunda brecha de rol cerrada por el mismo ítem del backlog: el
    // endpoint en sí (no solo la pantalla) rechaza a cualquier cuenta que no
    // sea Cliente, aunque tenga el entitlement de módulo DespachoHuevo.
    [Fact]
    public async Task UnTrabajadorNoPuedeConsultarElCreditoDelTenant()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "DespachoHuevo");

        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // Desglose visible del crédito (spec, ítem 2 del backlog): sin ajustes
    // la respuesta trae una lista vacía; con un ajuste persistido para ese
    // cliente, aparece con su motivo.
    [Fact]
    public async Task ElCreditoDelClienteIncluyeLosAjustesConMotivo()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();

        var sinAjustes = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, sinAjustes.StatusCode);
        var cuerpoSinAjustes = await sinAjustes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, cuerpoSinAjustes.GetProperty("ajustes").GetArrayLength());

        var ajuste = new AjusteCreditoHuevo(
            clienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 45m,
            "Corrección de precio Extra.", Guid.NewGuid());
        using (var alcance = _factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
            db.Add(ajuste);
            await db.SaveChangesAsync();
        }

        var conAjustes = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, conAjustes.StatusCode);
        var cuerpo = await conAjustes.Content.ReadFromJsonAsync<JsonElement>();
        var elementoAjuste = Assert.Single(cuerpo.GetProperty("ajustes").EnumerateArray());
        Assert.Equal(45m, elementoAjuste.GetProperty("monto").GetDecimal());
        Assert.Equal(
            "Corrección de precio Extra.", elementoAjuste.GetProperty("motivo").GetString());
    }
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Requiere Docker corriendo.
Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~DespachosHuevoCaisyEndpointsTests"`
Expected: PASA — toda la clase en verde, incluidos los dos tests nuevos.

- [ ] **Step 5: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs
git commit -m "test(avicola): cubrir los dos gates de rol del credito de huevo y los ajustes en el endpoint"
```

---

## Task 6: Frontend — tipos, componente `AjustesCreditoHuevo` y `PedidoFormularioPage.tsx`

**Files:**
- Modify: `web/src/features/despacho-huevo/api.ts`
- Create: `web/src/features/despacho-huevo/AjustesCreditoHuevo.tsx`
- Modify: `web/src/features/pedidos-alimento/PedidoFormularioPage.tsx`
- Modify: `web/src/features/pedidos-alimento/PedidoFormularioPage.test.tsx`

**Interfaces:**
- Produces:
  - `export interface AjusteCreditoHuevo { id: string; monto: number; motivo: string; fecha: string }` (`web/src/features/despacho-huevo/api.ts`).
  - `export interface BalanceCreditoHuevo { saldoDisponible: number; ajustes: AjusteCreditoHuevo[] }` (mismo archivo, reemplaza la forma anterior).
  - `export function AjustesCreditoHuevo({ ajustes }: { ajustes: AjusteCreditoHuevo[] }): JSX.Element | null` (`web/src/features/despacho-huevo/AjustesCreditoHuevo.tsx`) — no renderiza nada si `ajustes.length === 0`. La consume también la Tarea 7.

- [ ] **Step 1: Escribir el test que falla en `PedidoFormularioPage.test.tsx`**

En `web/src/features/pedidos-alimento/PedidoFormularioPage.test.tsx`, actualizar los dos mocks existentes de `GET /api/despachos-huevo/credito` para incluir `ajustes: []` (el contrato real del backend, después de la Tarea 3, siempre incluye ese campo — dejarlo afuera del mock haría que `credito.ajustes` llegue `undefined` al componente nuevo).

La línea 126 hoy es:

```tsx
      'GET /api/despachos-huevo/credito': respuesta(200, { saldoDisponible: -45.5 }),
```

Cambiarla a:

```tsx
      'GET /api/despachos-huevo/credito': respuesta(200, { saldoDisponible: -45.5, ajustes: [] }),
```

La línea 140 hoy es:

```tsx
      'GET /api/despachos-huevo/credito': respuesta(200, { saldoDisponible: 45.5 }),
```

Cambiarla a:

```tsx
      'GET /api/despachos-huevo/credito': respuesta(200, { saldoDisponible: 45.5, ajustes: [] }),
```

Agregar este test nuevo después de `'el Cliente ve el crédito por despachos de huevo, incluso negativo'` (después de su `});` de cierre):

```tsx
  test('el Cliente ve las correcciones aplicadas al crédito, con motivo', async () => {
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
      'GET /api/granjas': respuesta(200, []),
      'GET /api/despachos-huevo/credito': respuesta(200, {
        saldoDisponible: 45,
        ajustes: [{ id: 'a1', monto: 45, motivo: 'Corrección de precio Extra.', fecha: '2026-09-01' }],
      }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    expect(await screen.findByText(/Corrección de precio Extra\./)).toBeInTheDocument();
  });
```

- [ ] **Step 2: Correr los tests y verificar que el nuevo falla**

Run: `cd web && npx vitest run src/features/pedidos-alimento/PedidoFormularioPage.test.tsx`
Expected: el test nuevo (`el Cliente ve las correcciones...`) FALLA — el texto no se renderiza todavía. Los demás tests del archivo siguen en verde (el `ajustes: []` agregado no cambia nada visible).

- [ ] **Step 3: Extender los tipos (`api.ts`)**

En `web/src/features/despacho-huevo/api.ts`, el bloque actual es:

```ts
export interface BalanceCreditoHuevo {
  saldoDisponible: number;
}
```

Reemplazarlo por:

```ts
export interface AjusteCreditoHuevo {
  id: string;
  monto: number;
  motivo: string;
  fecha: string; // yyyy-MM-dd
}

export interface BalanceCreditoHuevo {
  saldoDisponible: number;
  ajustes: AjusteCreditoHuevo[];
}
```

- [ ] **Step 4: Crear el componente `AjustesCreditoHuevo.tsx`**

Crear `web/src/features/despacho-huevo/AjustesCreditoHuevo.tsx` con este contenido completo:

```tsx
import { Stack, Typography } from '@mui/material';
import type { AjusteCreditoHuevo } from './api';
import { formatoFecha, formatoMoneda } from './constantes';

interface Props {
  ajustes: AjusteCreditoHuevo[];
}

// Desglose visible del crédito (spec, ítem 2 del backlog): solo los ajustes
// de corrección con su motivo, el único componente del cálculo que
// representa una sorpresa real para el cliente. Reusado en
// PedidoFormularioPage.tsx y PedidoAlimentoDetallePage.tsx.
export function AjustesCreditoHuevo({ ajustes }: Props) {
  if (ajustes.length === 0) return null;
  return (
    <Stack spacing={0.5} sx={{ mt: 1 }}>
      <Typography variant="caption" color="text.secondary">
        Correcciones aplicadas a tu crédito:
      </Typography>
      {ajustes.map((a) => (
        <Typography key={a.id} variant="body2" color={a.monto < 0 ? 'error' : 'text.secondary'}>
          {formatoFecha(a.fecha)} — {a.motivo} ({formatoMoneda(a.monto)})
        </Typography>
      ))}
    </Stack>
  );
}
```

- [ ] **Step 5: Renderizarlo en `PedidoFormularioPage.tsx`**

En `web/src/features/pedidos-alimento/PedidoFormularioPage.tsx`, agregar el import nuevo justo después del de `obtenerBalanceCreditoHuevo` (línea 22):

```tsx
import { obtenerBalanceCreditoHuevo } from '../despacho-huevo/api';
import { AjustesCreditoHuevo } from '../despacho-huevo/AjustesCreditoHuevo';
```

El bloque de render actual (dentro de `<Stack spacing={2}>`) es:

```tsx
          {esCliente && credito && (
            <Typography
              variant="body2"
              color={credito.saldoDisponible < 0 ? 'error' : 'text.secondary'}
            >
              Crédito por despachos de huevo: {formatoMoneda(credito.saldoDisponible)}
            </Typography>
          )}
```

Reemplazarlo por (se envuelve en un `Box`, no en un fragmento `<>...</>`: el contenedor padre es un `<Stack spacing={2}>` y MUI aplica ese espaciado a cada hijo directo — un fragmento dejaría los dos elementos como hijos separados y metería 2 unidades de espacio entre el saldo y sus correcciones, mientras que el `Box` los mantiene como un bloque único, con el layout del resto de la página igual que hoy):

```tsx
          {esCliente && credito && (
            <Box>
              <Typography
                variant="body2"
                color={credito.saldoDisponible < 0 ? 'error' : 'text.secondary'}
              >
                Crédito por despachos de huevo: {formatoMoneda(credito.saldoDisponible)}
              </Typography>
              <AjustesCreditoHuevo ajustes={credito.ajustes} />
            </Box>
          )}
```

`Box` ya está importado en este archivo (línea 4 del bloque de imports de `@mui/material`), no hay que agregarlo.

- [ ] **Step 6: Correr los tests y verificar que pasan**

Run: `cd web && npx vitest run src/features/pedidos-alimento/PedidoFormularioPage.test.tsx`
Expected: PASA — todos los tests del archivo en verde, incluido el nuevo.

- [ ] **Step 7: Lint y type-check del frontend**

Run: `cd web && npm run lint && npx tsc --noEmit`
Expected: sin errores.

- [ ] **Step 8: Commit**

```bash
git add web/src/features/despacho-huevo/api.ts web/src/features/despacho-huevo/AjustesCreditoHuevo.tsx web/src/features/pedidos-alimento/PedidoFormularioPage.tsx web/src/features/pedidos-alimento/PedidoFormularioPage.test.tsx
git commit -m "feat(web): mostrar las correcciones del credito de huevo en el formulario de pedido"
```

---

## Task 7: Frontend — `PedidoAlimentoDetallePage.tsx`: gateo por rol + ajustes en el diálogo de SP9E

**Files:**
- Modify: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx`
- Modify: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`

**Interfaces:**
- Consumes: `AjustesCreditoHuevo` y `AjusteCreditoHuevo`/`BalanceCreditoHuevo` (Tarea 6), `useAuth` (`web/src/features/auth/AuthContext.tsx`, ya existe: `tieneRol(...roles: Rol[]): boolean`).
- Produces: ninguna interfaz nueva para otras tareas — es la última pieza de la feature.

- [ ] **Step 1: Mockear `AuthContext` y escribir los tests que fallan**

En `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`, este archivo hoy **no** mockea `../auth/AuthContext` (el componente todavía no usa `useAuth`). Agregar, justo después del import del componente (línea 5, `import { PedidoAlimentoDetallePage } from './PedidoAlimentoDetallePage';`), el mismo mock que ya usa `PedidoFormularioPage.test.tsx` — por defecto Cliente, así que ningún test existente cambia de comportamiento:

```tsx
// AuthContext real es pesado para este test: se mockea useAuth. Por defecto
// Cliente, que es el rol que puede ver y confirmar el crédito de huevo.
const authMock = vi.fn(() => ({ tieneRol: (...roles: string[]) => roles.includes('Cliente') }));
vi.mock('../auth/AuthContext', () => ({
  useAuth: () => authMock(),
}));
```

Actualizar el mock existente de `GET /api/despachos-huevo/credito` dentro de `'enviar con credito insuficiente pide confirmar y reintenta con el flag'` (línea 212 hoy: `if (ruta === 'GET /api/despachos-huevo/credito') return respuesta(200, { saldoDisponible: -5000 });`) para incluir `ajustes: []`:

```tsx
      if (ruta === 'GET /api/despachos-huevo/credito')
        return respuesta(200, { saldoDisponible: -5000, ajustes: [] });
```

Agregar estos dos tests nuevos después de `'enviar con credito insuficiente pide confirmar y reintenta con el flag'` (después de su `});` de cierre):

```tsx
  test('el diálogo de confirmación muestra las correcciones aplicadas al crédito', async () => {
    const usuario = userEvent.setup();
    const cuerpos: unknown[] = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      const ruta = `${req.method} ${new URL(req.url).pathname}`;
      if (ruta === 'POST /api/pedidos-alimento/p1/enviar') {
        cuerpos.push(await req.json());
        return cuerpos.length === 1 ? respuesta(409, { title: 'Crédito insuficiente' }) : respuesta(204);
      }
      if (ruta === 'GET /api/pedidos-alimento/p1') return respuesta(200, pedidoBorradorDevuelto);
      if (ruta === 'GET /api/despachos-huevo/credito') {
        return respuesta(200, {
          saldoDisponible: -5000,
          ajustes: [{ id: 'a1', monto: 45, motivo: 'Corrección de precio Extra.', fecha: '2026-09-01' }],
        });
      }
      return respuesta(404);
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Enviar a CAISY' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar envío' }));

    expect(await screen.findByText(/Corrección de precio Extra\./)).toBeInTheDocument();
  });

  test('un Trabajador no ve montos ni puede confirmar un envío con crédito insuficiente', async () => {
    // mockReturnValue (no "once"): el componente vuelve a llamar useAuth()
    // en cada re-render (hay varios durante este test, por los clics y las
    // queries que resuelven), así que la sobreescritura tiene que persistir
    // durante todo el test, no solo la primera llamada.
    authMock.mockReturnValue({ tieneRol: () => false });
    const usuario = userEvent.setup();
    const rutasLlamadas: string[] = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      const ruta = `${req.method} ${new URL(req.url).pathname}`;
      rutasLlamadas.push(ruta);
      if (ruta === 'POST /api/pedidos-alimento/p1/enviar') {
        return respuesta(409, { title: 'Crédito insuficiente' });
      }
      if (ruta === 'GET /api/pedidos-alimento/p1') return respuesta(200, pedidoBorradorDevuelto);
      return respuesta(404);
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Enviar a CAISY' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar envío' }));

    expect(
      await screen.findByText('Este pedido necesita confirmación del Cliente para continuar.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar de todas formas' })).not.toBeInTheDocument();
    expect(screen.queryByText(/va a dejar tu crédito/i)).not.toBeInTheDocument();
    expect(rutasLlamadas).not.toContain('GET /api/despachos-huevo/credito');
  });
```

- [ ] **Step 2: Correr los tests y verificar que los nuevos fallan**

Run: `cd web && npx vitest run src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`
Expected: los dos tests nuevos FALLAN (el componente todavía no tiene gateo por rol, así que "Este pedido necesita confirmación del Cliente..." no existe, y el texto de las correcciones tampoco). Los demás tests del archivo siguen en verde.

- [ ] **Step 3: Agregar el gateo por rol en `PedidoAlimentoDetallePage.tsx`**

Agregar los imports nuevos. El bloque de imports actual (líneas 24-40) es:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { ApiError } from '../../lib/http';
import { obtenerBalanceCreditoHuevo } from '../despacho-huevo/api';
import {
  borrarPedido,
  enviarPedido,
  obtenerOriginalDocumentoNota,
  obtenerPedido,
  obtenerPrecioVigente,
  obtenerVistaDocumentoNota,
  recibirPedido,
  type DocumentoNota,
  type LineaPedidoDetalle,
} from './api';
```

Cambiarlo a:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../../lib/http';
import { AjustesCreditoHuevo } from '../despacho-huevo/AjustesCreditoHuevo';
import { obtenerBalanceCreditoHuevo } from '../despacho-huevo/api';
import {
  borrarPedido,
  enviarPedido,
  obtenerOriginalDocumentoNota,
  obtenerPedido,
  obtenerPrecioVigente,
  obtenerVistaDocumentoNota,
  recibirPedido,
  type DocumentoNota,
  type LineaPedidoDetalle,
} from './api';
```

Dentro de la función `PedidoAlimentoDetallePage`, la línea actual es:

```tsx
  const queryClient = useQueryClient();
```

Cambiarla a:

```tsx
  const queryClient = useQueryClient();
  const { tieneRol } = useAuth();
  const esCliente = tieneRol('Cliente');
```

- [ ] **Step 4: Gatear la query `creditoParaConfirmar`**

El bloque actual es:

```tsx
  const { data: creditoParaConfirmar } = useQuery({
    queryKey: ['despachos-huevo', 'credito'],
    queryFn: obtenerBalanceCreditoHuevo,
    enabled: requiereConfirmacionCredito,
  });
```

Cambiarlo a (el backend ahora rechaza con 403 a quien no es Cliente, así que dejar la query habilitada para un Trabajador dispararía un fetch que falla en el uso normal de la aplicación, no solo en un caso límite):

```tsx
  const { data: creditoParaConfirmar } = useQuery({
    queryKey: ['despachos-huevo', 'credito'],
    queryFn: obtenerBalanceCreditoHuevo,
    enabled: requiereConfirmacionCredito && esCliente,
  });
```

- [ ] **Step 5: Gatear el contenido del diálogo y el botón de confirmación**

El bloque actual dentro del `Dialog` es:

```tsx
            {requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                Este pedido va a dejar tu crédito por despachos de huevo en{' '}
                {formatoMoneda((creditoParaConfirmar?.saldoDisponible ?? 0) - (totalParaEnviar ?? 0))}{' '}
                negativo (saldo actual {formatoMoneda(creditoParaConfirmar?.saldoDisponible ?? 0)}, este
                pedido {formatoMoneda(totalParaEnviar ?? 0)}). ¿Confirmás el envío igual?
              </Alert>
            )}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrarDialogoEnvio}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => enviar.mutate(requiereConfirmacionCredito)}
            disabled={enviar.isPending || totalParaEnviar === null}
          >
            {requiereConfirmacionCredito ? 'Enviar de todas formas' : 'Confirmar envío'}
          </Button>
        </DialogActions>
```

Cambiarlo a:

```tsx
            {esCliente && requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                Este pedido va a dejar tu crédito por despachos de huevo en{' '}
                {formatoMoneda((creditoParaConfirmar?.saldoDisponible ?? 0) - (totalParaEnviar ?? 0))}{' '}
                negativo (saldo actual {formatoMoneda(creditoParaConfirmar?.saldoDisponible ?? 0)}, este
                pedido {formatoMoneda(totalParaEnviar ?? 0)}). ¿Confirmás el envío igual?
                <AjustesCreditoHuevo ajustes={creditoParaConfirmar?.ajustes ?? []} />
              </Alert>
            )}
            {!esCliente && requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                Este pedido necesita confirmación del Cliente para continuar.
              </Alert>
            )}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrarDialogoEnvio}>Cancelar</Button>
          {(esCliente || !requiereConfirmacionCredito) && (
            <Button
              variant="contained"
              onClick={() => enviar.mutate(requiereConfirmacionCredito)}
              disabled={enviar.isPending || totalParaEnviar === null}
            >
              {requiereConfirmacionCredito ? 'Enviar de todas formas' : 'Confirmar envío'}
            </Button>
          )}
        </DialogActions>
```

- [ ] **Step 6: Correr los tests y verificar que pasan**

Run: `cd web && npx vitest run src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`
Expected: PASA — todos los tests del archivo en verde, incluidos los dos nuevos.

- [ ] **Step 7: Lint y type-check del frontend**

Run: `cd web && npm run lint && npx tsc --noEmit`
Expected: sin errores.

- [ ] **Step 8: Commit**

```bash
git add web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx
git commit -m "feat(web): el Trabajador no ve ni confirma el credito de huevo en el envio de pedidos"
```

---

## Task 8: Puerta de calidad completa y cierre

Última tarea: corre la suite completa (no solo los tests dirigidos de cada tarea), fija cualquier fallout, y cierra la feature. Requiere Docker corriendo.

**Files:** ninguno propio — solo verificación y, si `verify.ps1`/`verify.sh` encuentran algo, arreglos puntuales en los archivos ya tocados por las Tareas 1-7.

- [ ] **Step 1: Correr la puerta de calidad completa**

Run (PowerShell, Windows): `./verify.ps1`
Expected: 0 errores — frontend lint/build/tests, backend build, Architecture, Unit, GestorCaisy e Integration tests, todos en verde. Si algo falla, arreglar el contenido (nunca relajar el gate) y volver a correr `./verify.ps1` hasta que pase limpio.

- [ ] **Step 2: Actualizar el handoff**

Editar `docs/ai/HANDOFF.md`: mover el ítem 2 del backlog priorizado ("Desglose visible del crédito") de "Siguiente a brainstormear" a resuelto, con una línea breve describiendo qué se decidió (Opción A: solo ajustes con motivo, reusado en los dos lugares existentes, más las dos brechas de rol cerradas) — mismo formato que ya usa el ítem 1 del backlog (tachado con `~~...~~` y una nota "resuelto en..."). Actualizar también la sección "Estado" y "Pendiente inmediato" para reflejar que esta feature quedó implementada y verificada.

- [ ] **Step 3: Commit y push**

```bash
git add -A
git commit -m "$(cat <<'EOF'
docs(ai): cerrar el item 2 del backlog de credito de huevo en el handoff

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
git push
```
