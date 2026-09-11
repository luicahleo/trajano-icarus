# Bandeja de novedades del despacho de huevo en la PWA — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el Cliente vea en la PWA las novedades de sus despachos de huevo y de los ajustes de su crédito —que ya se generan hoy y ninguna pantalla muestra— y que el Trabajador del mismo tenant no vea las que llevan plata.

**Architecture:** No hay dominio, enum ni migración nuevos. Un tipo estático nuevo en Application resuelve, a partir del rol, el conjunto de tipos de notificación visibles; ese conjunto baja por el puerto hasta el repositorio y se aplica en SQL, así que el listado, el contador y el marcado como leída filtran por lo mismo y no pueden desincronizarse. El frontend agrega un bloque de novedades en `DespachosHuevoPage`, calcado del bloque "Novedades de CAISY" que ya funciona en `PedidosAlimentoPage`. Un cambio de una línea pone el monto del `Meta` del ajuste en cuatro decimales, consistente con el ítem 4 del backlog.

**Tech Stack:** .NET 10, MediatR, EF Core 9 (SQL Server), xUnit, NSubstitute, Testcontainers.MsSql, React 19 + MUI + TanStack Query, Vitest + Testing Library.

**Spec:** `docs/superpowers/specs/2026-09-11-sp9f-bandeja-novedades-despacho-huevo-pwa-design.md`

## Global Constraints

- **El gate de rol se escribe como exclusión del rol `Trabajador`, nunca como inclusión del rol `Cliente`.** El mismo handler sirve al grupo tenant y al grupo caisy (`DespachosHuevoEndpoints.cs:123-124`): un gate positivo de "solo Cliente" le saca a `GestorCaisy` y a `Administrador` sus notificaciones `CreditoInsuficiente` y rompe la bandeja ya desplegada de `Trajano.GestorCaisy` y sus pruebas.
- **El rol se compara con un literal string, no con el enum `Rol`.** `Icarus.GestionAvicola.Application` solo referencia su propio `Domain` y `BuildingBlocks.Application`; agregar una referencia a `Icarus.Identity.Domain` rompe la prueba `ReglasDeModulosTests.GestionAvicolaNoSeReferenciaConOtrosModulos`. Mismo criterio que el literal `"Cliente"` ya usado en `ObtenerBalanceCreditoHuevoHandler`.
- No crear valores de enum, columnas, entidades ni migraciones. `NotificacionInternaDespachoHuevo`, `TipoNotificacionDespachoHuevo`, `ConfiguracionNotificacionInternaDespachoHuevo` y `GestionAvicolaDbContext` quedan intactos.
- No modificar nada bajo `Icarus/src/Apps/Trajano.GestorCaisy/` ni sus vistas. Su bandeja ya existe y esta feature no la altera.
- No agregar sondeo periódico, uso del parámetro `since` ni manejo de ETag en el cliente. No agregar cola offline ni IndexedDB: la bandeja de despachos es deliberadamente online.
- `TreatWarningsAsErrors=true` en `Icarus/Directory.Build.props`: cualquier advertencia rompe el build.
- Identificadores de dominio, comentarios y textos de interfaz en español correcto, con acentos, UTF-8 sin BOM. Nunca mojibake.
- Anti-PII: ninguna notificación lleva datos nominales; no registrar en logs montos, saldos ni motivos de ajuste.
- Prohibido `--no-verify`. Prohibido relajar baselines, umbrales o exclusiones de `quality/`.
- La puerta de calidad completa (`./verify.ps1`) exige Docker corriendo: los tests de integración usan Testcontainers.MsSql.

## Estructura de archivos

**Crear:**

| Archivo | Responsabilidad |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/VisibilidadNotificacionesDespachoHuevo.cs` | Única fuente de verdad de qué tipos ve cada rol |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/VisibilidadNotificacionesDespachoHuevoTests.cs` | La regla y la red que obliga a clasificar tipos nuevos |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs` | Los tres handlers de la bandeja (hoy sin ninguna prueba) |
| `Icarus/tests/Icarus.IntegrationTests/NotificacionesDespachoHuevoEndpointsTests.cs` | Visibilidad real del endpoint tenant con sesión de Cliente y de Trabajador |

**Modificar:**

| Archivo | Cambio |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/PuertosNotificacionesDespachoHuevo.cs` | `ListarAsync` y `ContarNoLeidasAsync` reciben el conjunto de tipos visibles |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs` | Filtro de tipo en las dos consultas |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs` | Los tres handlers derivan el conjunto del rol |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs:434` | `{monto:0.00}` → `{monto:0.0000}` |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs` | Aserción del `Meta` con cuatro decimales |
| `web/src/features/despacho-huevo/api.ts` | Tipo y dos funciones de la bandeja |
| `web/src/features/despacho-huevo/api.test.ts` | Rutas exactas de las dos funciones |
| `web/src/features/despacho-huevo/constantes.ts` | `mensajeNotificacionDespachoHuevo` |
| `web/src/features/despacho-huevo/DespachosHuevoPage.tsx` | Bloque de novedades sobre la tabla |
| `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx` | Render, marcado como leída y ausencia del bloque |
| `docs/ai/HANDOFF.md` | Cierre del ítem 5 y reescritura del ítem nuevo de saldo negativo |

**No se toca:** `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs` (los dos endpoints y el ETag ya sirven el caso; el contador que devuelve se calcula desde el listado ya filtrado), `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs` (la interfaz no cambia de nombre), ninguna entidad de dominio, ninguna migración.

**Call sites del puerto que cambian de firma:** exactamente dos, los dos en `ComandosNotificacionesDespachoHuevo.cs` (líneas 24 y 38). Los cuatro archivos de prueba que crean un `Substitute.For<INotificacionesInternasDespachoHuevo>()` (`ConfirmarRecepcionDespachoHuevoHandlerTests`, `CorregirPublicacionPrecioHuevoHandlerTests`, `NotificacionesInternasTests`, `PedidosAlimentoHandlerTests`) solo usan `Agregar` y no se tocan.

---

### Task 1: Regla de visibilidad por rol

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/VisibilidadNotificacionesDespachoHuevo.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/VisibilidadNotificacionesDespachoHuevoTests.cs`

**Interfaces:**
- Consumes: `TipoNotificacionDespachoHuevo` (enum ya existente en `Icarus.GestionAvicola.Domain`, valores `DespachoRecibido = 0`, `CreditoInsuficiente = 1`, `AjusteCredito = 2`).
- Produces: `VisibilidadNotificacionesDespachoHuevo.Para(string? rol)` → `IReadOnlyCollection<TipoNotificacionDespachoHuevo>`, y la constante pública `VisibilidadNotificacionesDespachoHuevo.RolTrabajador` (valor `"Trabajador"`). Las tareas 2 y 3 usan exactamente esa firma.

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/VisibilidadNotificacionesDespachoHuevoTests.cs`:

```csharp
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9F: la bandeja de notificaciones del despacho de huevo esconde los tipos
// financieros al rol Trabajador y no cambia nada para los demás roles. La
// regla es una EXCLUSIÓN del Trabajador, no una inclusión del Cliente: el
// mismo handler sirve al grupo tenant y al grupo caisy, así que un gate
// positivo de «solo Cliente» le sacaría a GestorCaisy sus notificaciones.
public class VisibilidadNotificacionesDespachoHuevoTests
{
    [Fact]
    public void ElTrabajadorNoVeLosTiposFinancieros()
    {
        var visibles = VisibilidadNotificacionesDespachoHuevo.Para("Trabajador");

        Assert.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido, visibles);
        Assert.DoesNotContain(TipoNotificacionDespachoHuevo.AjusteCredito, visibles);
        Assert.DoesNotContain(TipoNotificacionDespachoHuevo.CreditoInsuficiente, visibles);
    }

    [Theory]
    [InlineData("Cliente")]
    [InlineData("GestorCaisy")]
    [InlineData("Administrador")]
    [InlineData(null)]
    public void LosDemasRolesVenTodosLosTipos(string? rol)
    {
        var visibles = VisibilidadNotificacionesDespachoHuevo.Para(rol);

        Assert.Equal(Enum.GetValues<TipoNotificacionDespachoHuevo>().Length, visibles.Count);
        Assert.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido, visibles);
        Assert.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente, visibles);
        Assert.Contains(TipoNotificacionDespachoHuevo.AjusteCredito, visibles);
    }

    // Red de seguridad: si alguien agrega un valor al enum y no lo clasifica,
    // esta prueba falla y lo obliga a decidir si es financiero. La rama por
    // defecto de la clasificación es fail-closed (financiero), así que un
    // olvido esconde el tipo nuevo al Trabajador en vez de filtrárselo.
    [Fact]
    public void CadaTipoDelEnumEstaClasificadoExplicitamente()
    {
        var esperados = new Dictionary<TipoNotificacionDespachoHuevo, bool>
        {
            [TipoNotificacionDespachoHuevo.DespachoRecibido] = false,
            [TipoNotificacionDespachoHuevo.CreditoInsuficiente] = true,
            [TipoNotificacionDespachoHuevo.AjusteCredito] = true,
        };

        Assert.Equal(
            Enum.GetValues<TipoNotificacionDespachoHuevo>().OrderBy(t => t).ToArray(),
            esperados.Keys.OrderBy(t => t).ToArray());

        var visiblesTrabajador = VisibilidadNotificacionesDespachoHuevo.Para(
            VisibilidadNotificacionesDespachoHuevo.RolTrabajador);
        foreach (var (tipo, esFinanciero) in esperados)
            Assert.Equal(!esFinanciero, visiblesTrabajador.Contains(tipo));
    }
}
```

- [ ] **Step 2: Correr las pruebas para verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~VisibilidadNotificacionesDespachoHuevoTests`
Expected: FAIL de compilación — el tipo `VisibilidadNotificacionesDespachoHuevo` no existe.

- [ ] **Step 3: Escribir la implementación mínima**

Crear `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/VisibilidadNotificacionesDespachoHuevo.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

// Visibilidad de la bandeja de notificaciones del despacho de huevo por rol
// (spec SP9F).
//
// La regla se expresa como EXCLUSIÓN del rol Trabajador y no como inclusión
// del rol Cliente, a propósito: DespachosHuevoEndpoints mapea los mismos dos
// endpoints en el grupo tenant y en el grupo caisy, así que los dos alcances
// comparten handler. Un gate positivo de «solo Cliente» —como el de
// ObtenerBalanceCreditoHuevoHandler, que sí es exclusivo del tenant— le
// sacaría a GestorCaisy y a Administrador sus notificaciones
// CreditoInsuficiente y rompería la bandeja ya desplegada de
// Trajano.GestorCaisy.
//
// El rol viaja como string y se compara con un literal: este proyecto solo
// referencia su propio Domain y BuildingBlocks.Application, y la prueba
// ReglasDeModulosTests.GestionAvicolaNoSeReferenciaConOtrosModulos impide
// referenciar Icarus.Identity.Domain para usar el enum Rol.
public static class VisibilidadNotificacionesDespachoHuevo
{
    public const string RolTrabajador = "Trabajador";

    private static readonly TipoNotificacionDespachoHuevo[] Todos =
        Enum.GetValues<TipoNotificacionDespachoHuevo>();

    private static readonly TipoNotificacionDespachoHuevo[] SinFinancieros =
        [.. Todos.Where(tipo => !EsFinanciero(tipo))];

    public static IReadOnlyCollection<TipoNotificacionDespachoHuevo> Para(string? rol) =>
        string.Equals(rol, RolTrabajador, StringComparison.Ordinal) ? SinFinancieros : Todos;

    // Financiero = lleva plata o el id de un pedido en Meta. El Trabajador
    // nunca lo ve, aunque tenga el entitlement del módulo: misma regla de
    // negocio que CreditoHuevoRequiereRolClienteException.
    //
    // La rama por defecto es fail-closed a propósito: un tipo nuevo sin
    // clasificar se trata como financiero y queda oculto al Trabajador en vez
    // de filtrársele. CadaTipoDelEnumEstaClasificadoExplicitamente avisa en
    // rojo cuando eso pasa.
    private static bool EsFinanciero(TipoNotificacionDespachoHuevo tipo) => tipo switch
    {
        TipoNotificacionDespachoHuevo.DespachoRecibido => false,
        TipoNotificacionDespachoHuevo.CreditoInsuficiente => true,
        TipoNotificacionDespachoHuevo.AjusteCredito => true,
        _ => true,
    };
}
```

- [ ] **Step 4: Correr las pruebas para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~VisibilidadNotificacionesDespachoHuevoTests`
Expected: PASS, 6 pruebas (1 + 4 del `Theory` + 1).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/VisibilidadNotificacionesDespachoHuevo.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/VisibilidadNotificacionesDespachoHuevoTests.cs
git commit -m "feat(avicola): regla de visibilidad por rol de la bandeja de despacho de huevo"
```

---

### Task 2: El conjunto de tipos visibles baja al repositorio

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/PuertosNotificacionesDespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs` (líneas 24 y 38)
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `VisibilidadNotificacionesDespachoHuevo.Para(string? rol)` de la Task 1.
- Produces: las firmas nuevas del puerto, que la Task 3 no usa y la Task 5 ejercita por HTTP:
  - `ListarAsync(Guid? clienteId, IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles, CancellationToken)`
  - `ContarNoLeidasAsync(Guid? clienteId, IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles, CancellationToken)`

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9F: los tres handlers de la bandeja del despacho de huevo derivan del rol
// el conjunto de tipos visibles y lo bajan al repositorio, para que el
// listado y el contador no puedan desincronizarse. Hasta esta feature no
// había ninguna prueba de estos handlers.
public class NotificacionesDespachoHuevoHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly INotificacionesInternasDespachoHuevo _repositorio =
        Substitute.For<INotificacionesInternasDespachoHuevo>();

    private static ICurrentUser Usuario(string? rol, Guid? clienteId)
    {
        var usuario = Substitute.For<ICurrentUser>();
        usuario.Rol.Returns(rol);
        usuario.ClienteId.Returns(clienteId);
        usuario.UsuarioId.Returns(Guid.NewGuid());
        return usuario;
    }

    [Fact]
    public async Task ElListadoDelTrabajadorPideSoloLosTiposOperativos()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Trabajador", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido)
                && !tipos.Contains(TipoNotificacionDespachoHuevo.AjusteCredito)
                && !tipos.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElListadoDelClientePideTodosLosTipos()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Cliente", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Count == Enum.GetValues<TipoNotificacionDespachoHuevo>().Length),
            Arg.Any<CancellationToken>());
    }

    // Regresión de la bandeja global ya desplegada: una cuenta de CAISY lleva
    // ClienteId nulo y tiene que seguir viendo CreditoInsuficiente.
    [Fact]
    public async Task ElListadoDeCaisyConservaElAlcanceGlobalYLosTiposFinancieros()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("GestorCaisy", null))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            null,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElListadoOrdenaDeLoMasNuevoALoMasViejo()
    {
        var vieja = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(Guid.NewGuid(), ClienteId);
        var nueva = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([vieja, nueva]);

        var resultado = await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Cliente", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("AjusteCredito", resultado[0].Tipo);
        Assert.Equal("27.0000 Bs — Precio mal digitado.", resultado[0].Meta);
    }

    [Fact]
    public async Task ElContadorDelTrabajadorPideSoloLosTiposOperativos()
    {
        await new ContarNotificacionesDespachoHuevoNoLeidasHandler(
                _repositorio, Usuario("Trabajador", ClienteId))
            .Handle(new ContarNotificacionesDespachoHuevoNoLeidasQuery(), CancellationToken.None);

        await _repositorio.Received(1).ContarNoLeidasAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                !tipos.Contains(TipoNotificacionDespachoHuevo.AjusteCredito)),
            Arg.Any<CancellationToken>());
    }
}
```

Nota para quien ejecuta: `ParaRecepcionConfirmada` y `ParaAjusteCredito` sellan `FechaUtc` con `DateTime.UtcNow` en el constructor, así que la creada segunda es la más nueva y el orden esperado del listado es `AjusteCredito` primero.

- [ ] **Step 2: Correr las pruebas para verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~NotificacionesDespachoHuevoHandlerTests`
Expected: FAIL de compilación — `ListarAsync` y `ContarNoLeidasAsync` todavía toman dos parámetros.

- [ ] **Step 3: Cambiar las firmas del puerto**

Reemplazar el contenido completo de `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/PuertosNotificacionesDespachoHuevo.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public interface INotificacionesInternasDespachoHuevo
{
    void Agregar(NotificacionInternaDespachoHuevo notificacion);

    Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    // tiposVisibles llega resuelto por rol desde
    // VisibilidadNotificacionesDespachoHuevo y se aplica en SQL (spec SP9F):
    // el listado y el contador filtran por lo mismo, así que no pueden
    // desincronizarse ni traer filas que el usuario no puede ver.
    Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Aplicar el filtro en el repositorio**

Reemplazar el contenido completo de `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs`:

```csharp
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioNotificacionesInternasDespachoHuevo(GestionAvicolaDbContext db)
    : INotificacionesInternasDespachoHuevo
{
    public void Agregar(NotificacionInternaDespachoHuevo notificacion) =>
        db.NotificacionesInternasDespachoHuevo.Add(notificacion);

    public async Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    // El filtro de tipo se traduce a un IN sobre una columna int
    // (HasConversion<int> en la configuración de EF) y se aplica sobre un
    // conjunto ya acotado por el índice (ClienteId, FechaUtc): no justifica
    // un índice propio.
    public async Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .Where(n => n.ClienteId == clienteId && tiposVisibles.Contains(n.Tipo))
            .ToListAsync(cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .CountAsync(
                n => n.ClienteId == clienteId && !n.Leida && tiposVisibles.Contains(n.Tipo),
                cancellationToken);
}
```

- [ ] **Step 5: Pasar el conjunto desde los dos handlers**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs`, reemplazar el cuerpo de `ListarNotificacionesDespachoHuevoHandler.Handle` (línea 24):

```csharp
        (await repositorio.ListarAsync(
                usuarioActual.ClienteId,
                VisibilidadNotificacionesDespachoHuevo.Para(usuarioActual.Rol),
                cancellationToken))
```

y el cuerpo de `ContarNotificacionesDespachoHuevoNoLeidasHandler.Handle` (línea 38):

```csharp
        repositorio.ContarNoLeidasAsync(
            usuarioActual.ClienteId,
            VisibilidadNotificacionesDespachoHuevo.Para(usuarioActual.Rol),
            cancellationToken);
```

El resto del archivo no cambia. `VisibilidadNotificacionesDespachoHuevo` está en el mismo namespace, así que no hace falta un `using` nuevo.

- [ ] **Step 6: Correr las pruebas para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~NotificacionesDespachoHuevoHandlerTests`
Expected: PASS, 5 pruebas.

- [ ] **Step 7: Correr la suite unitaria completa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests`
Expected: PASS. Si algún archivo de prueba no compila por la firma nueva, es un call site que este plan no previó: arreglarlo pasando `VisibilidadNotificacionesDespachoHuevo.Para(...)` o `Enum.GetValues<TipoNotificacionDespachoHuevo>()`, nunca relajando la firma.

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/PuertosNotificacionesDespachoHuevo.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs
git commit -m "feat(avicola): filtrar la bandeja de despacho de huevo por tipo visible segun rol"
```

---

### Task 3: El marcado como leída valida el tipo

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs` (`MarcarNotificacionDespachoHuevoLeidaHandler`)
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `VisibilidadNotificacionesDespachoHuevo.Para(string? rol)` de la Task 1; `INotificacionesInternasDespachoHuevo.ObtenerPorIdAsync` (firma sin cambios).
- Produces: nada que consuman otras tareas.

- [ ] **Step 1: Escribir las pruebas que fallan**

Agregar al final de la clase `NotificacionesDespachoHuevoHandlerTests` (antes de la llave de cierre):

```csharp
    [Fact]
    public async Task ElTrabajadorNoPuedeMarcarLeidaUnaNotificacionFinanciera()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarcarNotificacionDespachoHuevoLeidaHandler(
                    _repositorio, Usuario("Trabajador", ClienteId), unidadTrabajo)
                .Handle(
                    new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                    CancellationToken.None));

        Assert.False(notificacion.Leida);
        await unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElTrabajadorSiPuedeMarcarLeidaUnaNotificacionOperativa()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            Guid.NewGuid(), ClienteId);
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await new MarcarNotificacionDespachoHuevoLeidaHandler(
                _repositorio, Usuario("Trabajador", ClienteId), unidadTrabajo)
            .Handle(
                new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                CancellationToken.None);

        Assert.True(notificacion.Leida);
        await unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElClienteSiPuedeMarcarLeidaUnaNotificacionFinanciera()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await new MarcarNotificacionDespachoHuevoLeidaHandler(
                _repositorio, Usuario("Cliente", ClienteId), unidadTrabajo)
            .Handle(
                new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                CancellationToken.None);

        Assert.True(notificacion.Leida);
        await unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
```

Y agregar los `using` que falten al principio del archivo de pruebas:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
```

(`NotFoundException` vive en `Icarus.BuildingBlocks.Domain`; `IUnidadTrabajoGestionAvicola`, en `Icarus.GestionAvicola.Application`.)

- [ ] **Step 2: Correr las pruebas para verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~NotificacionesDespachoHuevoHandlerTests`
Expected: FAIL en `ElTrabajadorNoPuedeMarcarLeidaUnaNotificacionFinanciera` — no se lanza `NotFoundException`, la notificación queda leída. Las otras dos pasan.

- [ ] **Step 3: Escribir la implementación mínima**

En `ComandosNotificacionesDespachoHuevo.cs`, dentro de `MarcarNotificacionDespachoHuevoLeidaHandler.Handle`, insertar la guarda inmediatamente después del cruce de `ClienteId`:

```csharp
        if (notificacion.ClienteId != usuarioActual.ClienteId)
            throw new NotFoundException("Notificación", request.NotificacionId);
        // Misma regla de visibilidad que el listado (spec SP9F): un tipo que
        // el rol no puede ver tampoco se puede marcar leída, o un Trabajador
        // le haría desaparecer al Cliente un AjusteCredito antes de que lo
        // lea. El 404 genérico no revela que la notificación existe, igual
        // que el cruce de tenant de la línea anterior.
        if (!VisibilidadNotificacionesDespachoHuevo.Para(usuarioActual.Rol)
                .Contains(notificacion.Tipo))
            throw new NotFoundException("Notificación", request.NotificacionId);
```

- [ ] **Step 4: Correr las pruebas para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~NotificacionesDespachoHuevoHandlerTests`
Expected: PASS, 8 pruebas.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesDespachoHuevoHandlerTests.cs
git commit -m "fix(avicola): el Trabajador no puede marcar leida una notificacion financiera"
```

---

### Task 4: El monto del `Meta` del ajuste con cuatro decimales

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs:434`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: nada de tareas anteriores.
- Produces: el formato `"27.0000 Bs — Precio mal digitado."`, que la Task 7 muestra crudo en la PWA.

- [ ] **Step 1: Escribir la prueba que falla**

En `CorregirPublicacionPrecioHuevoHandlerTests`, dentro de `ConfirmarCorrigeLaErroneaPublicaLaCorrectivaYAjustaLosRecibidos`, reemplazar la aserción de la notificación por esta versión, que además fija el `Meta`:

```csharp
        // El Meta lleva el monto con CUATRO decimales (ítem 4 del backlog: el
        // crédito de huevo se muestra con cuatro en toda la aplicación, y
        // AjustesCreditoHuevo.tsx usa formatoMonedaExacta para el mismo
        // monto). La PWA muestra este texto crudo, así que el formato es
        // contrato de presentación.
        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInternaDespachoHuevo>(n =>
            n.Tipo == TipoNotificacionDespachoHuevo.AjusteCredito &&
            n.DespachoHuevoId == despacho.Id && n.ClienteId == clienteId &&
            n.Meta == "27.0000 Bs — Precio mal digitado."));
```

- [ ] **Step 2: Correr la prueba para verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ConfirmarCorrigeLaErroneaPublicaLaCorrectivaYAjustaLosRecibidos`
Expected: FAIL — el `Meta` real dice `"27.00 Bs — Precio mal digitado."`.

- [ ] **Step 3: Escribir la implementación mínima**

En `ComandosPreciosHuevo.cs`, línea 434, cambiar el especificador de formato:

```csharp
                string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{monto:0.0000} Bs — {request.Motivo[..Math.Min(request.Motivo.Length, 470)]}")));
```

No tocar el truncado de 470: la prueba existente `UnMotivoLargoNoDesbordaElMetaDeLaNotificacion` asegura `meta!.Length <= 500`, y 470 más `" Bs — "` deja 24 caracteres para el monto, de sobra para dos decimales más.

- [ ] **Step 4: Correr las pruebas para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~CorregirPublicacionPrecioHuevoHandlerTests`
Expected: PASS, incluida `UnMotivoLargoNoDesbordaElMetaDeLaNotificacion`.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs
git commit -m "fix(avicola): el meta del ajuste de credito con cuatro decimales"
```

---

### Task 5: Visibilidad real del endpoint tenant

**Files:**
- Create: `Icarus/tests/Icarus.IntegrationTests/NotificacionesDespachoHuevoEndpointsTests.cs`

**Interfaces:**
- Consumes: el comportamiento de las tareas 2 y 3, por HTTP.
- Produces: nada que consuman otras tareas.

**Requisito:** Docker corriendo (Testcontainers.MsSql).

**Nota de convención:** los archivos de prueba de integración duplican sus helpers a propósito para quedar autocontenidos —hay un comentario que lo dice explícitamente en `DespachosHuevoCaisyEndpointsTests.cs:152-155`—. Este archivo sigue esa convención en vez de compartir helpers.

- [ ] **Step 1: Escribir la prueba que falla**

Crear `Icarus/tests/Icarus.IntegrationTests/NotificacionesDespachoHuevoEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9F: la bandeja de notificaciones del despacho de huevo del tenant esconde
// los tipos financieros al rol Trabajador y se los muestra completos al
// Cliente. Las notificaciones se siembran directo en el DbContext: el
// objetivo de esta clase es la visibilidad por rol, no el flujo que las
// origina (cubierto por CorreccionPrecioHuevoTests y
// DespachosHuevoCaisyEndpointsTests).
[Collection(IntegracionCollection.Nombre)]
public class NotificacionesDespachoHuevoEndpointsTests
{
    private readonly IdentityFactory _factory;

    public NotificacionesDespachoHuevoEndpointsTests(IdentityFactory factory) => _factory = factory;

    private static async Task<string> LoginComo(HttpClient cliente, string email, string? contrasena = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = contrasena ?? IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Pedido(
        HttpMethod metodo, string url, string token, HttpContent? contenido = null) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = contenido,
        };

    // Tenant nuevo con el módulo GestionAvicola (habilita DespachoHuevo para
    // el Cliente). Duplicado a propósito para que el archivo quede
    // autocontenido, igual que en DespachosHuevoCaisyEndpointsTests.
    private async Task<(HttpClient Cliente, string Token, Guid ClienteId)> CrearClienteConGestionAvicolaAsync()
    {
        var cliente = _factory.CreateClient();
        var tokenAdmin = await LoginComo(cliente, SemillaIdentidad.EmailAdmin);
        var email = $"notificaciones-huevo-cliente-{Guid.NewGuid():N}@icarus.test";
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
        var email = $"notificaciones-huevo-trabajador-{Guid.NewGuid():N}@icarus.test";
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

    private async Task SembrarNotificacionesAsync(Guid clienteId, Guid despachoId)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
        db.NotificacionesInternasDespachoHuevo.AddRange(
            NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(despachoId, clienteId),
            NotificacionInternaDespachoHuevo.ParaAjusteCredito(
                despachoId, clienteId, "27.0000 Bs — Precio mal digitado."));
        await db.SaveChangesAsync();
    }

    private static async Task<(List<string> Tipos, int Contador)> LeerBandejaAsync(
        HttpClient cliente, string token)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo/notificaciones", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var tipos = cuerpo.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("tipo").GetString()!)
            .ToList();
        return (tipos, cuerpo.GetProperty("contador").GetInt32());
    }

    [Fact]
    public async Task ElClienteVeLosDosTiposYElTrabajadorSoloElOperativo()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "DespachoHuevo");
        await SembrarNotificacionesAsync(clienteId, Guid.NewGuid());

        var (tiposCliente, contadorCliente) = await LeerBandejaAsync(cliente, tokenCliente);
        var (tiposTrabajador, contadorTrabajador) = await LeerBandejaAsync(cliente, tokenTrabajador);

        Assert.Contains("DespachoRecibido", tiposCliente);
        Assert.Contains("AjusteCredito", tiposCliente);
        Assert.Equal(2, contadorCliente);

        Assert.Contains("DespachoRecibido", tiposTrabajador);
        Assert.DoesNotContain("AjusteCredito", tiposTrabajador);
        Assert.Equal(1, contadorTrabajador);
    }

    [Fact]
    public async Task ElTrabajadorNoPuedeMarcarLeidaLaNotificacionDeAjuste()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "DespachoHuevo");
        await SembrarNotificacionesAsync(clienteId, Guid.NewGuid());

        var (_, _) = await LeerBandejaAsync(cliente, tokenCliente);
        var respuestaCliente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo/notificaciones", tokenCliente));
        var cuerpo = await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>();
        var idAjuste = cuerpo.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("tipo").GetString() == "AjusteCredito")
            .GetProperty("id").GetGuid();

        var intento = await cliente.SendAsync(Pedido(HttpMethod.Post,
            $"/api/despachos-huevo/notificaciones/{idAjuste}/marcar-leida", tokenTrabajador));

        Assert.Equal(HttpStatusCode.NotFound, intento.StatusCode);
    }
}
```

- [ ] **Step 2: Correr las pruebas para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~NotificacionesDespachoHuevoEndpointsTests`
Expected: PASS, 2 pruebas. (Este paso valida por HTTP el trabajo de las tareas 2 y 3; si falla, el problema está en esas tareas, no en la prueba.)

- [ ] **Step 3: Correr la suite de integración completa**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS. Las clases comparten la base de la colección: si una prueba de otra clase se pone en rojo, revisar que este archivo no publique precios ni cree despachos —no lo hace— antes de sospechar del cambio.

- [ ] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/NotificacionesDespachoHuevoEndpointsTests.cs
git commit -m "test(avicola): visibilidad por rol de la bandeja de despacho de huevo por HTTP"
```

---

### Task 6: Cliente HTTP y mensajes de la bandeja en la PWA

**Files:**
- Modify: `web/src/features/despacho-huevo/api.ts`
- Modify: `web/src/features/despacho-huevo/api.test.ts`
- Modify: `web/src/features/despacho-huevo/constantes.ts`

**Interfaces:**
- Consumes: el contrato del endpoint, ya existente: `GET /api/despachos-huevo/notificaciones` devuelve `{ items, contador }`, donde cada item es `{ id, tipo, despachoHuevoId, fechaUtc, leida, meta }`.
- Produces: `NotificacionDespachoHuevo`, `listarNotificacionesDespachoHuevo()`, `marcarNotificacionDespachoHuevoLeida(id)` y `mensajeNotificacionDespachoHuevo(tipo)`. La Task 7 usa exactamente esos nombres.

- [ ] **Step 1: Escribir las pruebas que fallan**

Agregar al final de `web/src/features/despacho-huevo/api.test.ts`, dentro del `describe`:

```ts
  test('listarNotificacionesDespachoHuevo consulta la bandeja del tenant', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, { items: [], contador: 0 }));
    vi.stubGlobal('fetch', f);
    await listarNotificacionesDespachoHuevo();
    expect(solicitud(f).url).toContain('/api/despachos-huevo/notificaciones');
  });

  test('marcarNotificacionDespachoHuevoLeida hace POST sobre la notificación', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await marcarNotificacionDespachoHuevoLeida('n1');
    expect(solicitud(f).method).toBe('POST');
    expect(solicitud(f).url).toContain('/api/despachos-huevo/notificaciones/n1/marcar-leida');
  });
```

Y agregar los dos nombres al `import` de `./api` al principio del archivo (orden alfabético, como ya está):

```ts
import {
  borrarDespacho,
  crearDespacho,
  despacharDespacho,
  editarDespacho,
  listarDespachos,
  listarNotificacionesDespachoHuevo,
  marcarNotificacionDespachoHuevoLeida,
  obtenerDespacho,
  obtenerPrecioHuevoVigente,
} from './api';
```

- [ ] **Step 2: Correr las pruebas para verificar que fallan**

Run: `cd web && npx vitest run src/features/despacho-huevo/api.test.ts`
Expected: FAIL — no existen las exportaciones importadas.

- [ ] **Step 3: Escribir la implementación mínima**

Agregar al final de `web/src/features/despacho-huevo/api.ts`:

```ts
// Bandeja de novedades del tenant (spec SP9F): los endpoints ya existían
// desde SP9C y hasta ahora ninguna pantalla los consumía. El backend filtra
// por rol qué tipos devuelve, así que acá no hay que decidir nada: se muestra
// lo que llega. `contador` viene del backend ya filtrado.
export interface NotificacionDespachoHuevo {
  id: string;
  tipo: string;
  despachoHuevoId: string | null;
  fechaUtc: string;
  leida: boolean;
  meta: string | null;
}

export const listarNotificacionesDespachoHuevo = () =>
  peticion<{ items: NotificacionDespachoHuevo[]; contador: number }>({
    ruta: '/despachos-huevo/notificaciones',
  });

export const marcarNotificacionDespachoHuevoLeida = (id: string) =>
  peticion<void>({
    ruta: `/despachos-huevo/notificaciones/${id}/marcar-leida`,
    metodo: 'POST',
  });
```

Agregar a `web/src/features/despacho-huevo/constantes.ts`, antes del bloque de reexportación de formatos:

```ts
// Mensajes de la bandeja de novedades (spec SP9F). Mismo patrón que
// mensajeNotificacion en pedidos-alimento/constantes.ts. El detalle del
// ajuste viaja en `meta` como texto plano y la página lo muestra crudo: no
// hace falta parsearlo.
export function mensajeNotificacionDespachoHuevo(tipo: string): string {
  switch (tipo) {
    case 'DespachoRecibido':
      return 'CAISY confirmó la recepción de un despacho de huevo.';
    case 'AjusteCredito':
      return 'Se ajustó tu crédito de huevo por una corrección de precio.';
    case 'CreditoInsuficiente':
      return 'Se envió un pedido de alimento con crédito de huevo insuficiente.';
    default:
      return 'Hubo una novedad en un despacho de huevo.';
  }
}
```

- [ ] **Step 4: Correr las pruebas para verificar que pasan**

Run: `cd web && npx vitest run src/features/despacho-huevo/api.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/despacho-huevo/api.ts web/src/features/despacho-huevo/api.test.ts web/src/features/despacho-huevo/constantes.ts
git commit -m "feat(web): cliente http y mensajes de la bandeja de despacho de huevo"
```

---

### Task 7: Bloque de novedades en `DespachosHuevoPage`

**Files:**
- Modify: `web/src/features/despacho-huevo/DespachosHuevoPage.tsx`
- Modify: `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx`

**Interfaces:**
- Consumes: `listarNotificacionesDespachoHuevo`, `marcarNotificacionDespachoHuevoLeida`, `NotificacionDespachoHuevo` y `mensajeNotificacionDespachoHuevo` de la Task 6.
- Produces: nada que consuman otras tareas.

- [ ] **Step 1: Escribir las pruebas que fallan**

Agregar a `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx`, dentro del `describe`:

```tsx
  test('muestra las novedades del despacho de huevo y permite marcarlas como leídas', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo': respuesta(200, []),
      'GET /api/despachos-huevo/notificaciones': respuesta(200, {
        items: [
          {
            id: 'n1',
            tipo: 'AjusteCredito',
            despachoHuevoId: 'h1',
            fechaUtc: '2026-09-10T15:00:00Z',
            leida: false,
            meta: '27.0000 Bs — Precio mal digitado.',
          },
        ],
        contador: 1,
      }),
      'POST /api/despachos-huevo/notificaciones/n1/marcar-leida': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();

    expect(await screen.findByText(/Se ajustó tu crédito de huevo/i)).toBeInTheDocument();
    expect(screen.getByText(/27,0000|27\.0000/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Marcar como leída' }));
    const marco = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/notificaciones/n1/marcar-leida');
    });
    expect(marco).toBe(true);
  });

  test('no muestra el bloque de novedades cuando no hay sin leer', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo': respuesta(200, []),
        'GET /api/despachos-huevo/notificaciones': respuesta(200, {
          items: [
            {
              id: 'n1',
              tipo: 'DespachoRecibido',
              despachoHuevoId: 'h1',
              fechaUtc: '2026-09-10T15:00:00Z',
              leida: true,
              meta: null,
            },
          ],
          contador: 0,
        }),
      }),
    );
    renderPagina();

    expect(await screen.findByText('No hay despachos todavía. Creá el primero.')).toBeInTheDocument();
    expect(screen.queryByText(/Novedades del despacho de huevo/i)).not.toBeInTheDocument();
  });
```

Nota para quien ejecuta: el texto `'No hay despachos todavía. Creá el primero.'` es el `mensajeVacio` que ya tiene `DespachosHuevoPage.tsx` (verificado). No cambiarlo.

- [ ] **Step 2: Correr las pruebas para verificar que fallan**

Run: `cd web && npx vitest run src/features/despacho-huevo/DespachosHuevoPage.test.tsx`
Expected: FAIL en la primera prueba — no existe el bloque ni el botón "Marcar como leída". La segunda puede pasar por casualidad (el bloque no existe): igual es válida como red de regresión.

- [ ] **Step 3: Escribir la implementación mínima**

En `web/src/features/despacho-huevo/DespachosHuevoPage.tsx`:

Reemplazar el bloque de imports del principio por:

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Chip,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import {
  listarDespachos,
  listarNotificacionesDespachoHuevo,
  marcarNotificacionDespachoHuevoLeida,
  type DespachoHuevoResumen,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  formatoFecha,
  formatoMoneda,
  mensajeNotificacionDespachoHuevo,
} from './constantes';
```

Dentro del componente, después del `useQuery` de despachos que ya existe, agregar:

```tsx
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // La bandeja de novedades ya se llenaba desde SP9C y ninguna pantalla la
  // mostraba (spec SP9F). El backend filtra por rol qué tipos devuelve: el
  // Trabajador no recibe los financieros.
  //
  // Deliberadamente sin `isError`: si la consulta falla, `notificaciones`
  // queda undefined, `sinLeer` vacío y el bloque no se renderiza, así que la
  // tabla de despachos —lo principal de la pantalla— sigue viva. La
  // degradación sale del patrón, no de código defensivo.
  const { data: notificaciones } = useQuery({
    queryKey: ['despachos-huevo', 'notificaciones'],
    queryFn: listarNotificacionesDespachoHuevo,
  });

  const marcarLeida = useMutation({
    mutationFn: marcarNotificacionDespachoHuevoLeida,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['despachos-huevo', 'notificaciones'] }),
  });

  const sinLeer = (notificaciones?.items ?? []).filter((n) => !n.leida);
```

Y dentro del `<Stack spacing={2}>` que envuelve el contenido, inmediatamente antes del `<TablaDatos .../>`, insertar:

```tsx
          {sinLeer.length > 0 && (
            <Paper variant="outlined" sx={{ mb: 1 }}>
              <Box sx={{ px: 2, pt: 1.5 }}>
                <Typography variant="subtitle2">
                  Novedades del despacho de huevo ({notificaciones?.contador})
                </Typography>
              </Box>
              <List dense>
                {sinLeer.slice(0, 5).map((n) => (
                  <ListItem
                    key={n.id}
                    secondaryAction={
                      <IconButton
                        edge="end"
                        aria-label="Marcar como leída"
                        onClick={() => marcarLeida.mutate(n.id)}
                        size="small"
                      >
                        <DoneAllRoundedIcon fontSize="small" />
                      </IconButton>
                    }
                    onClick={
                      n.despachoHuevoId
                        ? () => navigate(`/despachos/${n.despachoHuevoId}`)
                        : undefined
                    }
                    sx={{ cursor: n.despachoHuevoId ? 'pointer' : 'default' }}
                  >
                    <ListItemText
                      primary={mensajeNotificacionDespachoHuevo(n.tipo)}
                      secondary={
                        n.meta
                          ? `${n.meta} · ${new Date(n.fechaUtc).toLocaleString('es-BO')}`
                          : new Date(n.fechaUtc).toLocaleString('es-BO')
                      }
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}
```

El componente ya envuelve su contenido en un `<Stack spacing={2}>` dentro de `<EstadoCarga>` (verificado): el bloque entra como primer hijo de ese `Stack`, justo antes del `<TablaDatos>` existente. No hace falta crear el `Stack` ni reordenar nada más.

- [ ] **Step 4: Correr las pruebas para verificar que pasan**

Run: `cd web && npx vitest run src/features/despacho-huevo/DespachosHuevoPage.test.tsx`
Expected: PASS, 4 pruebas (las 2 que ya existían más las 2 nuevas). Las 2 existentes no stubean la ruta de notificaciones: el `fetchSimulado` devuelve 404, la query falla y el bloque no se renderiza, que es exactamente la degradación buscada.

- [ ] **Step 5: Correr lint, build y la suite completa del frontend**

Run: `cd web && npm run lint && npm run build && npx vitest run`
Expected: PASS, sin advertencias de lint ni errores de tipo.

- [ ] **Step 6: Commit**

```bash
git add web/src/features/despacho-huevo/DespachosHuevoPage.tsx web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx
git commit -m "feat(web): bandeja de novedades del despacho de huevo en la PWA"
```

---

### Task 8: Puerta de calidad y cierre del backlog

**Files:**
- Modify: `docs/ai/HANDOFF.md` (gitignoreado: se actualiza pero **no** se commitea)

- [ ] **Step 1: Correr la puerta de calidad completa**

Requiere Docker corriendo.

Run: `./verify.ps1`
Expected: todos los gates verdes. Cifras de referencia del estado previo a esta feature: frontend 285/285 en 61 archivos, backend 0 advertencias y 0 errores, Architecture 6/6, Unit 489/489, GestorCaisy 199/199, Integration 141/141. Esta feature agrega 11 pruebas unitarias, 2 de integración y 4 de frontend, así que los totales suben; GestorCaisy y Architecture deben quedar idénticos.

Si un gate falla, arreglar el contenido. Prohibido relajar una baseline, un umbral o una exclusión.

- [ ] **Step 2: Verificar que la bandeja de GestorCaisy no cambió**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS 199/199, sin una sola prueba tocada. Es la comprobación de que el gate de rol se escribió como exclusión del Trabajador y no como inclusión del Cliente.

- [ ] **Step 3: Actualizar el HANDOFF**

En `docs/ai/HANDOFF.md`:

- Marcar el ítem 5 del backlog como resuelto, con el reencuadre: el aviso proactivo del caso sorpresa ya se generaba desde SP9D y lo que faltaba era la pantalla; se construyó la bandeja en la PWA y se cerró la fuga de rol del Trabajador.
- Agregar un ítem nuevo al backlog, "Aviso explícito de saldo negativo", apuntando a la sección "Hallazgos de verificación" de la spec de SP9F: ahí está la tabla de los cuatro comandos que pueden bajar el saldo, la demostración de que el paso del tiempo nunca lo baja, y las tres semánticas anti-repetición posibles.
- Actualizar "Pendiente inmediato".

**No commitear `docs/ai/HANDOFF.md`:** está en `.gitignore`.

- [ ] **Step 4: Push**

```bash
git push origin develop
```

---

## Self-Review

**Cobertura de la spec:**

| Sección de la spec | Tarea |
|---|---|
| Decisión 1 (el bloque va en `DespachosHuevoPage`) | Task 7 |
| Decisión 2 (exclusión del Trabajador, no inclusión del Cliente) | Task 1, verificada en Task 8 Step 2 |
| Decisión 3 (regla en Application, rol como literal) | Task 1 |
| Decisión 4 (el conjunto baja al repositorio, filtro en SQL) | Task 2 |
| Decisión 5 (el contador recibe el mismo filtro, no se borra) | Task 2 |
| Decisión 6 (el marcado como leída valida el tipo) | Task 3 |
| Decisión 7 (`Meta` con cuatro decimales) | Task 4 |
| Decisión 8 (sin dominio, enum ni migración nuevos) | Restricción global; ninguna tarea los toca |
| Contrato / Application | Task 1, Task 2, Task 3 |
| Contrato / Infrastructure | Task 2 |
| Contrato / Host (sin cambios) | Ninguna tarea lo modifica; Task 5 lo ejercita |
| Contrato / Frontend | Task 6 |
| Presentación | Task 7 |
| Manejo de errores | Task 7 Step 3 (comentario) y Step 4 (las dos pruebas previas sin stub) |
| Pruebas unitarias | Tasks 1, 2, 3, 4 |
| Pruebas de integración | Task 5 |
| Pruebas de frontend | Tasks 6, 7 |
| Verificación | Task 8 |

Sin huecos.

**Placeholders:** ninguno. Todos los pasos con código llevan el código completo. El único punto donde el ejecutor tiene que decidir algo es un call site imprevisto del puerto (Task 2 Step 7), y ahí el plan fija el criterio: pasar el conjunto, nunca relajar la firma. El `mensajeVacio` de la tabla y la existencia del `Stack` envolvente quedaron verificados contra el archivo real y afirmados sin condicional.

**Consistencia de tipos y nombres:**

- `VisibilidadNotificacionesDespachoHuevo.Para(string?)` → `IReadOnlyCollection<TipoNotificacionDespachoHuevo>`: definido en Task 1, usado con esa firma exacta en Task 2 (dos handlers) y Task 3.
- `VisibilidadNotificacionesDespachoHuevo.RolTrabajador`: definido público en Task 1, usado en la prueba de Task 1.
- `ListarAsync` / `ContarNoLeidasAsync` con `(Guid?, IReadOnlyCollection<TipoNotificacionDespachoHuevo>, CancellationToken)`: declarados en Task 2 Step 3, implementados en Step 4, consumidos en Step 5, mockeados con esa misma aridad en Step 1.
- `listarNotificacionesDespachoHuevo`, `marcarNotificacionDespachoHuevoLeida`, `NotificacionDespachoHuevo`, `mensajeNotificacionDespachoHuevo`: definidos en Task 6, consumidos con esos nombres en Task 7.
- El literal `"27.0000 Bs — Precio mal digitado."` aparece en Task 2 Step 1, Task 3 Step 1, Task 4 Step 1, Task 5 Step 1 y Task 7 Step 1, siempre idéntico.
- `queryKey` `['despachos-huevo', 'notificaciones']`: idéntica en el `useQuery` y en el `invalidateQueries` de Task 7.

## Execution Handoff

Plan completo y guardado en `docs/superpowers/plans/2026-09-11-sp9f-bandeja-novedades-despacho-huevo-pwa.md`.

Orden de ejecución: las tareas 1 → 2 → 3 son secuenciales (cada una consume la anterior). La 4 es independiente y puede ir en cualquier momento. La 5 exige 2 y 3 hechas. La 6 es independiente del backend. La 7 exige la 6. La 8 va última.

Requisito de entorno: Docker corriendo para las tareas 5 y 8.
