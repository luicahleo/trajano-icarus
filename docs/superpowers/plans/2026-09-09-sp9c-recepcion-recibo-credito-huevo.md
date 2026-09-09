# SP9C — Recepción, recibo y crédito de huevo — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que `GestorRecepcionHuevos` confirme desde Trajano.GestorCaisy la
recepción de un despacho `Despachado`, generando un recibo PDF imprimible y
notificando al emisor; y que el monto recibido se convierta en crédito del
cliente, disponible dos semanas después, con una advertencia (no bloqueante)
al enviar un pedido de alimento cuando el saldo proyectado queda negativo.
Cierra la máquina de estados de SP9: `Despachado → Recibido`.

**Architecture:** Extiende el agregado `DespachoHuevo` de SP9B con el método
`ConfirmarRecepcion` (sin recuento por línea: el transportista solo verifica
visualmente, no hay reconteo formal). El recibo PDF reutiliza el patrón
QuestPDF de `ReciboPedidoRendererQuestPdf` (SP8D). Las notificaciones
internas usan una entidad **dedicada** (`NotificacionInternaDespachoHuevo`),
no la `NotificacionInterna` de SP8, porque esa clase tiene `PedidoId` como
`Guid` no anulable, mapeado en índice y expuesto en el DTO
`NotificacionResumen` — generalizarla exigiría una migración de una tabla ya
en producción para un beneficio menor al costo. El balance de crédito es una
consulta cruzada de solo lectura (sin tabla de saldo persistida) que suma
`DespachoHuevo` recibidos con crédito ya disponible y resta
`RecepcionPedidoAlimento.TotalRecibido` de pedidos con recepción real — el
único punto de acoplamiento entre SP9 y el `EnviarPedidoAlimentoHandler` ya
existente de SP8.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core (SQL Server),
MediatR, FluentValidation, QuestPDF (ya referenciado desde SP8D), ASP.NET
Core MVC (Trajano.GestorCaisy), xUnit + NSubstitute, Testcontainers.MsSql.

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake.
- Anti-PII: Seq recibe solo ids técnicos, estados y conteos.
- TDD estricto; un commit por tarea; `./verify.ps1` antes de cada commit;
  prohibido `--no-verify`. Docker activo para integración.
- `EstadoDespachoHuevo.Recibido` ya existe desde SP9B (`= 2`); esta tarea
  agrega el método que lo alcanza, no un valor de enum nuevo.
- El crédito disponible usa un desfase fijo de **14 días** desde
  `FechaRecepcion` (dos semanas, spec SP9) — constante, no configurable en
  esta versión.
- La advertencia de crédito insuficiente **no bloquea** el envío del pedido
  de alimento (decisión del usuario, spec SP9): es un efecto secundario
  (notificación a CAISY) dentro de la misma transacción del envío.

## Dependencia

SP9B integrado en `develop`, con al menos un despacho en estado `Despachado`
para poder confirmar recepción en pruebas de integración.

---

### Task 1: Dominio y persistencia — `ConfirmarRecepcion`

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs`

**Interfaces:**
- Produces: `DespachoHuevo.FechaRecepcion` (`DateOnly?`),
  `DespachoHuevo.ConfirmarRecepcion(DateOnly fechaRecepcion, Guid actorId)`;
  `IRepositorioDespachosHuevo.ListarPaginadoCaisyAsync(EstadoDespachoHuevo?
  estado, int saltar, int tomar, CancellationToken)` — para que Task 5 (API
  CAISY) lo consuma.

```csharp
// Agregar a DespachoHuevo.cs

    public DateOnly? FechaRecepcion { get; private set; }

    // Confirmación de CAISY (spec SP9): sin recuento por línea — el
    // transportista solo verifica visualmente, no hay reconteo formal en el
    // sistema. El monto ya quedó congelado al despachar; esta operación solo
    // cierra el estado y fija la fecha de recepción (fecha de negocio del
    // servidor, la resuelve el llamador).
    public void ConfirmarRecepcion(DateOnly fechaRecepcion, Guid actorId)
    {
        AsegurarEstado(EstadoDespachoHuevo.Despachado, "Solo un despacho despachado se puede recibir.");
        Estado = EstadoDespachoHuevo.Recibido;
        FechaRecepcion = fechaRecepcion;
        RegistrarTransicion(EstadoDespachoHuevo.Despachado, EstadoDespachoHuevo.Recibido, actorId);
    }
```

En `ConfiguracionDespachoHuevo.cs`, agregar junto a `FechaDespacho`:

```csharp
        builder.Property(d => d.FechaRecepcion).HasColumnType("date");
```

En `PuertosDespachosHuevo.cs`, agregar a la interfaz:

```csharp
    Task<(IReadOnlyList<DespachoHuevo> Items, int Total)> ListarPaginadoCaisyAsync(
        EstadoDespachoHuevo? estado, int saltar, int tomar,
        CancellationToken cancellationToken = default);
```

En `RepositorioDespachosHuevo.cs`, agregar la implementación (mismo patrón
que `RepositorioPedidosAlimento.ListarPaginadoCaisyAsync`):

```csharp
    public async Task<(IReadOnlyList<DespachoHuevo> Items, int Total)> ListarPaginadoCaisyAsync(
        EstadoDespachoHuevo? estado, int saltar, int tomar,
        CancellationToken cancellationToken = default)
    {
        var consulta = db.DespachosHuevo.Include(d => d.Detalles).AsNoTracking();
        if (estado is { } e)
            consulta = consulta.Where(d => d.Estado == e);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(d => d.FechaDespacho)
            .ThenByDescending(d => d.Id)
            .Skip(saltar).Take(tomar)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
```

- [ ] **Step 1: Escribir el test de dominio en rojo**

Agregar a `DespachoHuevoTests.cs`:

```csharp
    [Fact]
    public void ConfirmarRecepcionSoloDesdeDespachado()
    {
        var despacho = BorradorConDosDetalles();

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.ConfirmarRecepcion(new DateOnly(2026, 11, 6), ActorId));

        Assert.Equal("Solo un despacho despachado se puede recibir.", excepcion.Message);
    }

    [Fact]
    public void ConfirmarRecepcionCierraElEstadoYFijaLaFecha()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());

        despacho.ConfirmarRecepcion(new DateOnly(2026, 11, 6), ActorId);

        Assert.Equal(EstadoDespachoHuevo.Recibido, despacho.Estado);
        Assert.Equal(new DateOnly(2026, 11, 6), despacho.FechaRecepcion);
        Assert.Equal(2, despacho.Historial.Count);
    }

    [Fact]
    public void ConfirmarRecepcionDosVecesFalla()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());
        despacho.ConfirmarRecepcion(new DateOnly(2026, 11, 6), ActorId);

        Assert.Throws<ReglaNegocioException>(() =>
            despacho.ConfirmarRecepcion(new DateOnly(2026, 11, 7), ActorId));
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevo`
Expected: FAIL — `CS1061` (`ConfirmarRecepcion` no existe todavía).

- [ ] **Step 2: Implementar el método y la persistencia**

Con el contenido dado arriba.

- [ ] **Step 3: Generar la migración y correr integración**

```bash
dotnet ef migrations add DespachoHuevoRecepcion --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevo` →
PAST (13/13). `dotnet test Icarus/tests/Icarus.IntegrationTests` (Docker
activo) → PASS.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/
git commit -m "feat(avicola): confirmar recepcion de despachos de huevo"
```

---

### Task 2: Notificaciones internas dedicadas

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInternaDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TipoNotificacionDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/PuertosNotificacionesDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ComandosNotificacionesDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionNotificacionInternaDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesInternasDespachoHuevoTests.cs`

**Interfaces:**
- Produces: `NotificacionInternaDespachoHuevo`,
  `TipoNotificacionDespachoHuevo`, `INotificacionesInternasDespachoHuevo`,
  `ListarNotificacionesDespachoHuevoQuery`,
  `ContarNotificacionesDespachoHuevoNoLeidasQuery`,
  `MarcarNotificacionDespachoHuevoLeidaCommand` — para que Task 4 (comandos
  de recepción/crédito) y Task 5 (API) los consuman.

```csharp
// TipoNotificacionDespachoHuevo.cs
namespace Icarus.GestionAvicola.Domain;

// Tipos de notificación interna del despacho de huevo (spec SP9). Valores
// estables: agregar al final, nunca renumerar.
public enum TipoNotificacionDespachoHuevo
{
    DespachoRecibido = 0,
    CreditoInsuficiente = 1,
}
```

```csharp
// NotificacionInternaDespachoHuevo.cs
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Notificación interna del despacho de huevo (spec SP9). DespachoHuevoId es
// nulo para CreditoInsuficiente, que no se origina en un despacho sino en el
// envío de un pedido de alimento con saldo proyectado negativo; en ese caso
// el id del pedido va en Meta. Entidad dedicada y no una extensión de
// NotificacionInterna (SP8): esa clase tiene PedidoId no anulable, indexado
// y expuesto en su DTO — generalizarla implicaría migrar una tabla ya en
// producción para un beneficio menor al costo (ver nota de arquitectura del
// plan).
public sealed class NotificacionInternaDespachoHuevo : Entity
{
    private NotificacionInternaDespachoHuevo()
    {
    }

    private NotificacionInternaDespachoHuevo(
        TipoNotificacionDespachoHuevo tipo, Guid? despachoHuevoId, Guid? clienteId, string? meta)
    {
        Tipo = tipo;
        DespachoHuevoId = despachoHuevoId;
        ClienteId = clienteId;
        Meta = meta;
        FechaUtc = DateTime.UtcNow;
    }

    public static NotificacionInternaDespachoHuevo ParaRecepcionConfirmada(
        Guid despachoHuevoId, Guid clienteId) =>
        new(TipoNotificacionDespachoHuevo.DespachoRecibido, despachoHuevoId, clienteId, null);

    // Bandeja global de CAISY: el pedido de alimento afectado va en Meta
    // porque no hay un despacho de huevo específico que lo origine.
    public static NotificacionInternaDespachoHuevo ParaCreditoInsuficiente(Guid pedidoAlimentoId) =>
        new(TipoNotificacionDespachoHuevo.CreditoInsuficiente, null, null, pedidoAlimentoId.ToString());

    public TipoNotificacionDespachoHuevo Tipo { get; private set; }

    public Guid? DespachoHuevoId { get; private set; }

    public Guid? ClienteId { get; private set; }

    public string? Meta { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public bool Leida { get; private set; }

    public Guid? LeidaPor { get; private set; }

    public DateTime? FechaLeidaUtc { get; private set; }

    public void MarcarLeida(Guid actorId)
    {
        if (Leida)
            return;
        Leida = true;
        LeidaPor = actorId;
        FechaLeidaUtc = DateTime.UtcNow;
    }
}
```

```csharp
// PuertosNotificacionesDespachoHuevo.cs
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public interface INotificacionesInternasDespachoHuevo
{
    void Agregar(NotificacionInternaDespachoHuevo notificacion);

    Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);
}
```

```csharp
// ComandosNotificacionesDespachoHuevo.cs
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public sealed record ListarNotificacionesDespachoHuevoQuery
    : IRequest<IReadOnlyList<NotificacionDespachoHuevoResumen>>;

public sealed record NotificacionDespachoHuevoResumen(
    Guid Id, string Tipo, Guid? DespachoHuevoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record ContarNotificacionesDespachoHuevoNoLeidasQuery : IRequest<int>;

public sealed record MarcarNotificacionDespachoHuevoLeidaCommand(Guid NotificacionId) : IRequest;

public sealed class ListarNotificacionesDespachoHuevoHandler(
    INotificacionesInternasDespachoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ListarNotificacionesDespachoHuevoQuery, IReadOnlyList<NotificacionDespachoHuevoResumen>>
{
    public async Task<IReadOnlyList<NotificacionDespachoHuevoResumen>> Handle(
        ListarNotificacionesDespachoHuevoQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarAsync(usuarioActual.ClienteId, cancellationToken))
            .OrderByDescending(n => n.FechaUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificacionDespachoHuevoResumen(
                n.Id, n.Tipo.ToString(), n.DespachoHuevoId, n.FechaUtc, n.Leida, n.Meta))
            .ToList();
}

public sealed class ContarNotificacionesDespachoHuevoNoLeidasHandler(
    INotificacionesInternasDespachoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ContarNotificacionesDespachoHuevoNoLeidasQuery, int>
{
    public Task<int> Handle(
        ContarNotificacionesDespachoHuevoNoLeidasQuery request, CancellationToken cancellationToken) =>
        repositorio.ContarNoLeidasAsync(usuarioActual.ClienteId, cancellationToken);
}

public sealed class MarcarNotificacionDespachoHuevoLeidaHandler(
    INotificacionesInternasDespachoHuevo repositorio,
    ICurrentUser usuarioActual,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<MarcarNotificacionDespachoHuevoLeidaCommand>
{
    public async Task Handle(MarcarNotificacionDespachoHuevoLeidaCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación", request.NotificacionId);
        if (notificacion.ClienteId != usuarioActual.ClienteId)
            throw new NotFoundException("Notificación", request.NotificacionId);
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        notificacion.MarcarLeida(actorId);
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

**EF config** (mirror de `ConfiguracionNotificacionInterna.cs`):

```csharp
public sealed class ConfiguracionNotificacionInternaDespachoHuevo
    : IEntityTypeConfiguration<NotificacionInternaDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<NotificacionInternaDespachoHuevo> builder)
    {
        builder.ToTable("notificaciones_internas_despacho_huevo");
        builder.Property(n => n.Tipo).HasConversion<int>();
        builder.Property(n => n.Meta).HasMaxLength(500);
        builder.HasIndex(n => new { n.ClienteId, n.FechaUtc });
        builder.HasIndex(n => n.DespachoHuevoId);
    }
}
```

**Repositorio** (mirror de `RepositorioNotificacionesInternas.cs`):

```csharp
public sealed class RepositorioNotificacionesInternasDespachoHuevo(GestionAvicolaDbContext db)
    : INotificacionesInternasDespachoHuevo
{
    public void Agregar(NotificacionInternaDespachoHuevo notificacion) =>
        db.NotificacionesInternasDespachoHuevo.Add(notificacion);

    public async Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .Where(n => n.ClienteId == clienteId)
            .ToListAsync(cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .CountAsync(n => n.ClienteId == clienteId && !n.Leida, cancellationToken);
}
```

En `GestionAvicolaDbContext.cs`: agregar
`public DbSet<NotificacionInternaDespachoHuevo> NotificacionesInternasDespachoHuevo => Set<NotificacionInternaDespachoHuevo>();`.
En `DependencyInjection.cs`: agregar
`servicios.AddScoped<INotificacionesInternasDespachoHuevo, RepositorioNotificacionesInternasDespachoHuevo>();`.

- [ ] **Step 1: Escribir los tests de dominio en rojo**

```csharp
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class NotificacionesInternasDespachoHuevoTests
{
    [Fact]
    public void ParaRecepcionConfirmadaLlevaClienteYDespacho()
    {
        var despachoId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(despachoId, clienteId);

        Assert.Equal(TipoNotificacionDespachoHuevo.DespachoRecibido, notificacion.Tipo);
        Assert.Equal(despachoId, notificacion.DespachoHuevoId);
        Assert.Equal(clienteId, notificacion.ClienteId);
        Assert.False(notificacion.Leida);
    }

    [Fact]
    public void ParaCreditoInsuficienteEsGlobalSinDespacho()
    {
        var pedidoId = Guid.NewGuid();

        var notificacion = NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente(pedidoId);

        Assert.Equal(TipoNotificacionDespachoHuevo.CreditoInsuficiente, notificacion.Tipo);
        Assert.Null(notificacion.DespachoHuevoId);
        Assert.Null(notificacion.ClienteId);
        Assert.Equal(pedidoId.ToString(), notificacion.Meta);
    }

    [Fact]
    public void MarcarLeidaEsIdempotente()
    {
        var actorId = Guid.NewGuid();
        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(Guid.NewGuid(), Guid.NewGuid());

        notificacion.MarcarLeida(actorId);
        var primeraFecha = notificacion.FechaLeidaUtc;
        notificacion.MarcarLeida(Guid.NewGuid());

        Assert.True(notificacion.Leida);
        Assert.Equal(actorId, notificacion.LeidaPor);
        Assert.Equal(primeraFecha, notificacion.FechaLeidaUtc);
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter NotificacionesInternasDespachoHuevo`
Expected: FAIL — `CS0246`.

- [ ] **Step 2: Implementar dominio, puertos, comandos y persistencia**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter NotificacionesInternasDespachoHuevo`
Expected: PASS (3/3).

- [ ] **Step 3: Generar migración y correr integración**

```bash
dotnet ef migrations add NotificacionesDespachoHuevo --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host
```

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests` → PASS.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInternaDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TipoNotificacionDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/NotificacionesDespachoHuevo/ \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionNotificacionInternaDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesInternasDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesInternasDespachoHuevoTests.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/
git commit -m "feat(avicola): notificaciones internas de despacho de huevo"
```

---

### Task 3: Recibo PDF

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ReciboDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Documentos/ReciboDespachoHuevoRendererQuestPdf.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ReciboDespachoHuevoHandlerTests.cs`

**Interfaces:**
- Produces: `ObtenerReciboDespachoHuevoPdfQuery(Guid DespachoId):
  IRequest<byte[]>`, `IReciboDespachoHuevoRenderer` — para que Task 5 (API) y
  Task 6 (GestorCaisy) lo consuman.

```csharp
// ReciboDespachoHuevo.cs
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Recibo imprimible (spec SP9): consulta de solo lectura sobre datos ya
// persistidos, solo disponible una vez confirmada la recepción. No toca el
// agregado; se puede pedir cualquier cantidad de veces.
public sealed record ObtenerReciboDespachoHuevoPdfQuery(Guid DespachoId) : IRequest<byte[]>;

public interface IReciboDespachoHuevoRenderer
{
    Task<byte[]> RenderizarAsync(DespachoHuevo despacho, CancellationToken cancellationToken = default);
}

public sealed class ObtenerReciboDespachoHuevoPdfHandler(
    IRepositorioDespachosHuevo repositorio, IReciboDespachoHuevoRenderer renderer)
    : IRequestHandler<ObtenerReciboDespachoHuevoPdfQuery, byte[]>
{
    public async Task<byte[]> Handle(ObtenerReciboDespachoHuevoPdfQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Recibido)
            throw new NotFoundException("Recepción de despacho", request.DespachoId);
        return await renderer.RenderizarAsync(despacho, cancellationToken);
    }
}
```

```csharp
// ReciboDespachoHuevoRendererQuestPdf.cs — mirror de
// ReciboPedidoRendererQuestPdf.cs (SP8D), misma licencia QuestPDF Community.
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

public sealed class ReciboDespachoHuevoRendererQuestPdf : IReciboDespachoHuevoRenderer
{
    static ReciboDespachoHuevoRendererQuestPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderizarAsync(DespachoHuevo despacho, CancellationToken cancellationToken = default)
    {
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Text("Recibo de recepción de huevo").SemiBold().FontSize(18);

                pagina.Content().Column(columna =>
                {
                    columna.Spacing(8);
                    columna.Item().Text($"Fecha de despacho: {despacho.FechaDespacho:dd/MM/yyyy}");
                    columna.Item().Text($"Fecha de recepción: {despacho.FechaRecepcion:dd/MM/yyyy}");

                    columna.Item().PaddingTop(10).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });
                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Tamaño").SemiBold();
                            encabezado.Cell().Text("Amarras").SemiBold();
                            encabezado.Cell().Text("Huevos").SemiBold();
                            encabezado.Cell().Text("Precio").SemiBold();
                            encabezado.Cell().Text("Subtotal").SemiBold();
                        });
                        foreach (var linea in despacho.Detalles.OrderBy(d => d.Tamano))
                        {
                            tabla.Cell().Text(linea.Tamano.ToString());
                            tabla.Cell().Text($"{linea.CantidadAmarras} + {linea.UnidadesSueltas}");
                            tabla.Cell().Text(linea.CantidadHuevos.ToString());
                            tabla.Cell().Text(linea.PrecioProductorCongelado?.ToString("0.0000") ?? "—");
                            tabla.Cell().Text(linea.Subtotal?.ToString("0.00") ?? "—");
                        }
                    });

                    columna.Item().PaddingTop(10)
                        .Text($"Total amarras: {despacho.TotalAmarras}    Total huevos: {despacho.TotalHuevos}");
                    columna.Item()
                        .Text($"Total Bs: {despacho.TotalBs?.ToString("0.00") ?? "—"}").SemiBold();

                    columna.Item().PaddingTop(30).Row(fila =>
                    {
                        fila.RelativeItem().Column(firma =>
                        {
                            firma.Item().PaddingTop(30).LineHorizontal(1);
                            firma.Item().Text("Firma y sello de CAISY");
                        });
                    });
                });
            });
        });

        return Task.FromResult(documento.GeneratePdf());
    }
}
```

En `DependencyInjection.cs`: agregar
`servicios.AddScoped<IReciboDespachoHuevoRenderer, ReciboDespachoHuevoRendererQuestPdf>();`.

- [ ] **Step 1: Escribir el test del handler en rojo**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboDespachoHuevoHandlerTests
{
    [Fact]
    public async Task ExigeQueElDespachoEsteRecibido()
    {
        var repositorio = Substitute.For<IRepositorioDespachosHuevo>();
        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        repositorio.ObtenerConHistorialAsync(despacho.Id, Arg.Any<CancellationToken>())
            .Returns(despacho);
        var handler = new ObtenerReciboDespachoHuevoPdfHandler(
            repositorio, Substitute.For<IReciboDespachoHuevoRenderer>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerReciboDespachoHuevoPdfQuery(despacho.Id), CancellationToken.None));
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ReciboDespachoHuevo`
Expected: FAIL — `CS0246`.

- [ ] **Step 2: Implementar el query, el handler y el renderer**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ReciboDespachoHuevo`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ReciboDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Documentos/ReciboDespachoHuevoRendererQuestPdf.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/ReciboDespachoHuevoHandlerTests.cs
git commit -m "feat(avicola): recibo pdf de recepcion de huevo"
```

---

### Task 4: Confirmar recepción y balance de crédito

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`
  (solo `EnviarPedidoAlimentoHandler`)
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ConfirmarRecepcionDespachoHuevoHandlerTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/BalanceCreditoHuevoTests.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`
  (agregar casos de `EnviarPedidoAlimentoHandler` con la nueva dependencia)

**Interfaces:**
- Consumes: `DespachoHuevo.ConfirmarRecepcion` (Task 1),
  `INotificacionesInternasDespachoHuevo` (Task 2), `FechasNegocio.Hoy()`
  (`DespachosHuevo`, SP9B).
- Produces: `ConfirmarRecepcionDespachoHuevoCommand`,
  `IRepositorioBalanceCreditoHuevo`, `ObtenerBalanceCreditoHuevoQuery` — para
  que Task 5 (API) los invoque.

**Confirmar recepción** — agregar a `ComandosDespachosHuevo.cs`:

```csharp
public sealed record ConfirmarRecepcionDespachoHuevoCommand(Guid DespachoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.confirmar-recepcion", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed class ConfirmarRecepcionDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternasDespachoHuevo notificaciones)
    : IRequestHandler<ConfirmarRecepcionDespachoHuevoCommand>
{
    public async Task Handle(ConfirmarRecepcionDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Despachado)
            throw new ConflictException("Solo un despacho despachado se puede recibir.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        despacho.ConfirmarRecepcion(FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            despacho.Id, despacho.ClienteId));
        registroVuelo.Decidir("avicola.despachos-huevo.confirmar-recepcion", "recepcion", "aplicada",
            new Dictionary<string, object?> { ["TotalBs"] = despacho.TotalBs });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

(agregar `using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;`
al inicio del archivo).

**Balance de crédito** — nuevo puerto y consulta, en su propia carpeta
porque cruza `DespachosHuevo` y `PedidosAlimento`:

```csharp
// PuertoCreditoHuevo.cs
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
```

```csharp
// ComandosCreditoHuevo.cs
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

(agregar `using Icarus.GestionAvicola.Application.DespachosHuevo;` para
referenciar la `FechasNegocio` de esa carpeta, o duplicarla igual que las
otras — usar la misma decisión que Task 3 de SP9A: en este caso, como
`ComandosCreditoHuevo.cs` ya depende de `DespachosHuevo` para el balance en
sí, referenciar su `FechasNegocio` directamente es más simple que duplicarla
una tercera vez).

```csharp
// RepositorioBalanceCreditoHuevo.cs — cálculo directo contra el DbContext:
// cruza DespachoHuevo y PedidoAlimento/RecepcionPedidoAlimento, dos
// agregados del mismo módulo. El cálculo usa los campos primitivos
// (CantidadAmarras, UnidadesSueltas, PrecioProductorCongelado) en vez de las
// propiedades calculadas del dominio (CantidadHuevos, Subtotal) porque estas
// últimas no garantizan traducirse a SQL de forma fiable.
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioBalanceCreditoHuevo(GestionAvicolaDbContext db) : IRepositorioBalanceCreditoHuevo
{
    public async Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var fechaCorte = hoy.AddDays(-ReglasCreditoHuevo.DiasDisponibilidadCredito);

        var ingresos = await db.DespachosHuevo
            .Where(d => d.ClienteId == clienteId && d.Estado == EstadoDespachoHuevo.Recibido
                && d.FechaRecepcion != null && d.FechaRecepcion <= fechaCorte)
            .SelectMany(d => d.Detalles)
            .Where(det => det.PrecioProductorCongelado != null)
            .SumAsync(det =>
                (det.CantidadAmarras * 180 + det.UnidadesSueltas) * det.PrecioProductorCongelado!.Value,
                cancellationToken);

        var egresos = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.RecibidoConforme
                    || p.Estado == EstadoPedidoAlimento.RecibidoConDiferencias))
            .Select(p => p.Recepcion!.TotalRecibido)
            .SumAsync(cancellationToken);

        return ingresos - egresos;
    }
}
```

En `DependencyInjection.cs`: agregar
`servicios.AddScoped<IRepositorioBalanceCreditoHuevo, RepositorioBalanceCreditoHuevo>();`.

**Integración con `EnviarPedidoAlimentoHandler`** — único cambio en código
de SP8: agregar dos dependencias y, después de `pedido.EnviarACaisy(...)`,
comprobar el saldo proyectado y notificar sin bloquear:

```csharp
public sealed class EnviarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    IRepositorioNotificacionesPrecios repositorioPrecios,
    OpcionesPedidosAlimento opciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones,
    IRepositorioBalanceCreditoHuevo balanceCreditoHuevo,           // nuevo
    INotificacionesInternasDespachoHuevo notificacionesDespachoHuevo)  // nuevo
    : IRequestHandler<EnviarPedidoAlimentoCommand>
{
    public async Task Handle(EnviarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        // ... cuerpo existente sin cambios hasta pedido.EnviarACaisy(...) ...

        pedido.EnviarACaisy(hoy, actorId, precios);
        notificaciones.Agregar(NotificacionInterna.ParaCaisy(
            esReenvio ? TipoNotificacionPedido.PedidoReenviado : TipoNotificacionPedido.PedidoSolicitado,
            pedido.Id));

        // Advertencia de crédito (spec SP9): no bloquea el envío, solo avisa
        // a CAISY si el saldo proyectado del cliente queda negativo. El
        // saldo actual ya excluye este pedido porque todavía no está
        // Solicitado en la base al momento de leerlo (se lee antes del
        // SaveChanges de esta misma transacción).
        var saldoActual = await balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            pedido.ClienteId, hoy, cancellationToken);
        if (saldoActual - (pedido.TotalSolicitado ?? 0m) < 0)
            notificacionesDespachoHuevo.Agregar(
                NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente(pedido.Id));

        registroVuelo.Decidir("avicola.pedidos.enviar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = pedido.Detalles.Count,
                ["NotificacionPreciosId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        await transaccion.ConfirmarAsync(cancellationToken);
    }
}
```

(agregar `using Icarus.GestionAvicola.Application.CreditoHuevo;` y
`using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;` al
inicio de `ComandosPedidosAlimento.cs`). Verificar con
`Icarus.ArchitectureTests` que una referencia entre las carpetas
`PedidosAlimento` y `CreditoHuevo`/`NotificacionesDespachoHuevo` (mismo
proyecto `Icarus.GestionAvicola.Application`) no viola ninguna regla — las
reglas de capas de este proyecto separan módulos (`GestionAvicola` vs
`Clientes` vs `Identity`), no carpetas dentro del mismo módulo.

- [ ] **Step 1: Escribir los tests en rojo**

`ConfirmarRecepcionDespachoHuevoHandlerTests.cs`: cubrir `ConflictException`
si el estado no es `Despachado`, notificación agregada al confirmar, y
`NotFoundException` con id inexistente.

`BalanceCreditoHuevoTests.cs` (integración, requiere Testcontainers — ver
patrón de otros tests de integración del módulo): un despacho `Recibido` con
`FechaRecepcion` hace más de 14 días suma al saldo; uno con menos de 14 días
no suma; un pedido de alimento `RecibidoConforme` resta
`TotalRecibido`; un pedido en `Solicitado` (sin recepción real) no resta
nada.

Extender `PedidosAlimentoHandlerTests.cs`: `EnviarPedidoAlimentoHandler`
agrega una `NotificacionInternaDespachoHuevo.CreditoInsuficiente` cuando el
saldo mockeado es menor al total del pedido, y **no** la agrega ni bloquea
el envío cuando el saldo alcanza.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter
"ConfirmarRecepcionDespachoHuevo|BalanceCreditoHuevo|PedidosAlimentoHandler"`
Expected: FAIL — tipos/dependencias inexistentes, o el handler existente no
acepta las nuevas dependencias del constructor.

- [ ] **Step 2: Implementar comando, puerto, repositorio y la integración**

Run: mismo filtro que el Step 1.
Expected: PASS.

- [ ] **Step 3: Correr toda la suite (Docker activo)**

Run: `dotnet test Icarus/tests/Icarus.UnitTests`,
`dotnet test Icarus/tests/Icarus.IntegrationTests`,
`dotnet test Icarus/tests/Icarus.ArchitectureTests`
Expected: PASS — confirma que modificar `EnviarPedidoAlimentoHandler` no
rompió ninguna prueba existente de SP8.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/ConfirmarRecepcionDespachoHuevoHandlerTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/BalanceCreditoHuevoTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs
git commit -m "feat(avicola): confirmar recepcion de huevo y advertir credito insuficiente"
```

---

### Task 5: API — `/despachos-huevo-caisy` y crédito del tenant

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs` (sin cambios si ya se
  registró `api.MapDespachosHuevo();` en SP9B — verificar)
- Create: `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs`

**Interfaces:**
- Consumes: comandos/queries de Tasks 1-4.
- Produces: rutas HTTP bajo `/despachos-huevo-caisy` y
  `/despachos-huevo/credito` para que Trajano.GestorCaisy (Task 6) y la PWA
  las consuman.

Agregar a `DespachosHuevoEndpoints.cs` (mismo archivo de SP9B):

```csharp
        // Crédito disponible del tenant (spec SP9): informativo, no bloquea
        // el envío de pedidos de alimento.
        tenant.MapGet("/credito", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerBalanceCreditoHuevoQuery(), cancellationToken)));

        var politicaCaisy = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorRecepcionHuevos);
        var caisy = app.MapGroup("/despachos-huevo-caisy").RequireAuthorization(politicaCaisy);

        caisy.MapGet("/", async (ISender mediator, string? estado, int? pagina, int? tamanoPagina,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<EstadoDespachoHuevo>(estado, true, out var estadoParseado) && estado is not null)
                return Results.BadRequest(new { error = "El estado indicado no existe." });
            return Results.Ok(await mediator.Send(
                new ListarDespachosHuevoCaisyQuery(
                    estado is null ? null : estadoParseado, pagina ?? 1, tamanoPagina ?? 20),
                cancellationToken));
        });

        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoQuery(id), cancellationToken)));

        caisy.MapPost("/{id:guid}/confirmar-recepcion", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new ConfirmarRecepcionDespachoHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        caisy.MapGet("/{id:guid}/recibo.pdf", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var bytes = await mediator.Send(new ObtenerReciboDespachoHuevoPdfQuery(id), cancellationToken);
            return Results.File(bytes, "application/pdf", "recibo.pdf");
        });

        MapNotificacionesDespachoHuevo(tenant);
        MapNotificacionesDespachoHuevo(caisy);
```

y, al final de la clase, un helper análogo a `MapNotificaciones` de
`PedidosAlimentoEndpoints.cs` (mismo cálculo de ETag, records privados
distintos porque los tipos de notificación son los de
`NotificacionesDespachoHuevo`):

```csharp
    private static void MapNotificacionesDespachoHuevo(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/notificaciones", async Task<IResult> (
            ISender mediator, HttpContext contexto, DateTime? since,
            CancellationToken cancellationToken) =>
        {
            var notificaciones = await mediator.Send(new ListarNotificacionesDespachoHuevoQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones.Select(n => n.FechaUtc), contador);
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            contexto.Response.Headers.ETag = etag;
            var visibles = since is { } corte
                ? notificaciones.Where(n => n.FechaUtc > corte).ToList()
                : notificaciones;
            return Results.Ok(new { items = visibles, contador });
        });
        grupo.MapPost("/notificaciones/{id:guid}/marcar-leida", async (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new MarcarNotificacionDespachoHuevoLeidaCommand(id), cancellationToken);
            return Results.NoContent();
        });
    }

    private static string CalcularEtag(IEnumerable<DateTime> fechas, int contador)
    {
        var maxima = fechas.DefaultIfEmpty(DateTime.MinValue).Max();
        var huella = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{maxima.Ticks}:{contador}");
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(huella));
        return $"\"{Convert.ToHexString(bytes)[..16]}\"";
    }
```

Agregar el query de listado paginado CAISY a `ComandosDespachosHuevo.cs`
(Task 4 ya modificó ese archivo; este es un segundo cambio pequeño en el
mismo archivo):

```csharp
public sealed record ListarDespachosHuevoCaisyQuery(EstadoDespachoHuevo? Estado, int Pagina, int TamanoPagina)
    : IRequest<PaginaDespachosHuevo>;

public sealed record PaginaDespachosHuevo(IReadOnlyList<DespachoHuevoResumen> Items, int Total);

public sealed class ListarDespachosHuevoCaisyHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoCaisyQuery, PaginaDespachosHuevo>
{
    public async Task<PaginaDespachosHuevo> Handle(
        ListarDespachosHuevoCaisyQuery request, CancellationToken cancellationToken)
    {
        var saltar = (Math.Max(request.Pagina, 1) - 1) * Math.Max(request.TamanoPagina, 1);
        var (items, total) = await repositorio.ListarPaginadoCaisyAsync(
            request.Estado, saltar, Math.Max(request.TamanoPagina, 1), cancellationToken);
        return new PaginaDespachosHuevo(
            items.Select(d => new DespachoHuevoResumen(
                d.Id, d.Estado.ToString(), d.FechaDespacho, d.TotalAmarras, d.TotalHuevos, d.TotalBs)).ToList(),
            total);
    }
}
```

Agregar los `using` que falten (`Icarus.Identity.Domain`,
`Icarus.Identity.Infrastructure.Autenticacion`,
`Icarus.GestionAvicola.Application.CreditoHuevo`,
`Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo`) al
principio de `DespachosHuevoEndpoints.cs`.

- [ ] **Step 1: Escribir las pruebas de integración en rojo**

Cubrir: `GET /credito` del tenant devuelve `SaldoDisponible`; bandeja CAISY
lista y filtra por estado con paginación; detalle; `confirmar-recepcion`
pasa a `Recibido` y genera la notificación al tenant; segunda confirmación
da 409; `recibo.pdf` da 404 antes de confirmar y 200 con `content-type`
`application/pdf` después; sondeo de notificaciones con ETag en ambos
grupos; 403 con una cuenta CAISY que solo tiene `GestorPedidoAlimento`.

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter DespachosHuevoCaisy`
Expected: FAIL — 404 en las rutas nuevas.

- [ ] **Step 2: Implementar los endpoints**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter DespachosHuevoCaisy`
Expected: PASS.

- [ ] **Step 3: Correr toda la suite de integración**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs \
  Icarus/tests/Icarus.IntegrationTests/DespachosHuevoCaisyEndpointsTests.cs
git commit -m "feat(api): exponer recepcion, recibo y credito de huevo"
```

---

### Task 6: Trajano.GestorCaisy — bandeja de recepción de huevo

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Detalles.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/ConfirmarRecepcion.cshtml`
- Modify: menú/layout compartido (mismo archivo identificado en SP9A Task 6)
- Create: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/RecepcionesHuevoControllerTests.cs`
- Create: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoRecepcionesHuevoTests.cs`

**Interfaces:**
- Consumes: endpoints de Task 5 vía `IApiIcarusClient`;
  `ConstantesAutorizacion.PoliticaGestorRecepcionHuevos` (ya agregada en
  SP9A Task 1).

**`ApiIcarusClient`** — agregar DTOs y métodos análogos a los de
`PedidosController`
(`Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs:137-`),
apuntando a `despachos-huevo-caisy`:

```csharp
public sealed record DespachoHuevoResumenApi(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos, decimal? TotalBs);

public sealed record PaginaDespachosHuevoApi(IReadOnlyList<DespachoHuevoResumenApi> Items, int Total);

public sealed record DetalleDespachoHuevoApi(
    Guid Id, string Tamano, int CantidadAmarras, int UnidadesSueltas, int CantidadHuevos,
    decimal? PrecioProductorCongelado, decimal? Subtotal);

public sealed record DespachoHuevoDetalleApi(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos,
    decimal? TotalBs, IReadOnlyList<DetalleDespachoHuevoApi> Detalles);

public sealed record FiltrosDespachosHuevoApi(string? Estado, int Pagina, int TamanoPagina);
```

con métodos `ListarDespachosHuevoAsync(FiltrosDespachosHuevoApi filtros,
...)`, `ObtenerDespachoHuevoAsync(Guid id, ...)`,
`ConfirmarRecepcionDespachoHuevoAsync(Guid id, ...)`,
`ObtenerReciboDespachoHuevoPdfAsync(Guid id, ...)`,
`ListarNotificacionesDespachoHuevoAsync(...)`,
`MarcarNotificacionDespachoHuevoLeidaAsync(Guid id, ...)` — mismo cuerpo que
sus pares de `PedidosController`/`ApiIcarusClient`, cambiando la ruta base.

**`RecepcionesHuevoController.cs`** — mirror simplificado de
`PedidosController.cs` (sin las acciones de negociación: sin `Devolver`,
`Rechazar`, `Aceptar`, `EntregaEstimada`; sin subida de archivo porque CAISY
no sube nada en este flujo, solo confirma):

```csharp
[Route("RecepcionesHuevo")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorRecepcionHuevos)]
public sealed class RecepcionesHuevoController(IApiIcarusClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] FiltrosDespachosHuevoVista filtros, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        var pagina = await api.ListarDespachosHuevoAsync(
            new FiltrosDespachosHuevoApi(filtros.Estado, filtros.Pagina, filtros.TamanoPagina), token);
        var notificaciones = await api.ListarNotificacionesDespachoHuevoAsync(token);
        return View(new BandejaDespachosHuevoVista(pagina, filtros.Estado, notificaciones));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        DespachoHuevoDetalleApi despacho;
        try { despacho = await api.ObtenerDespachoHuevoAsync(id, token); }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound) { return NotFound(); }
        return View(new VistaDespachoHuevoDetalle(despacho, PuedeConfirmarse: despacho.Estado == "Despachado"));
    }

    [HttpGet("{id:guid}/ConfirmarRecepcion")]
    [ActionName("ConfirmarRecepcion")]
    public async Task<IActionResult> ConfirmarRecepcionPantalla(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        var despacho = await api.ObtenerDespachoHuevoAsync(id, token);
        if (despacho.Estado != "Despachado")
            return RedirectToAction(nameof(Detalles), new { id });
        return View("ConfirmarRecepcion", new VistaDespachoHuevoDetalle(despacho, PuedeConfirmarse: true));
    }

    [HttpPost("{id:guid}/ConfirmarRecepcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarRecepcion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            await api.ConfirmarRecepcionDespachoHuevoAsync(id, token);
            TempData["Exito"] = "La recepción quedó confirmada; el emisor fue notificado.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    [HttpGet("{id:guid}/Recibo")]
    public async Task<IActionResult> Recibo(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            var contenido = await api.ObtenerReciboDespachoHuevoPdfAsync(id, token);
            return File(contenido, "application/pdf", "recibo.pdf");
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound) { return NotFound(); }
    }

    [HttpPost("Notificaciones/{id:guid}/MarcarLeida")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        try { await api.MarcarNotificacionDespachoHuevoLeidaAsync(id, token); }
        catch (ErrorApiException error) when (error.Estado is 400 or 404 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Index));
    }
}
```

**Vistas**: `Index.cshtml` (bandeja con filtro de estado y paginación,
igual patrón visual que `Views/Pedidos/Index.cshtml`), `Detalles.cshtml`
(tabla por tamaño con amarras/unidades sueltas/huevos/precio/subtotal,
enlace al recibo si `Estado == "Recibido"`), `ConfirmarRecepcion.cshtml`
(resumen + botón de confirmación, sin campos de formulario porque no hay
motivo ni recuento).

**Menú**: agregar un ítem "Recepción de huevo" gateado por
`ReclamosCaisy.TieneGestorRecepcionHuevos(User)`, junto a "Precios de huevo"
(SP9A) y "Pedidos"/"Precios de alimento" (SP8).

- [ ] **Step 1: Escribir las pruebas del controller y del flujo en rojo**

Mirror de `PedidosControllerTests.cs`/`FlujoPedidosTests.cs`. Cubrir como
mínimo: acceso denegado sin `GestorRecepcionHuevos`; bandeja con filtro y
paginación; detalle; confirmar recepción con GET+POST y su mensaje de
éxito; descarga del recibo solo tras confirmar; notificaciones y marcado de
lectura.

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter RecepcionesHuevo`
Expected: FAIL — controller/vistas inexistentes.

- [ ] **Step 2: Implementar cliente, controller, vistas y menú**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter RecepcionesHuevo`
Expected: PASS.

- [ ] **Step 3: Correr toda la suite del proyecto MVC**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS — `GestorPedidoAlimento` y `GestorRecepcionHuevos` (precios,
SP9A) siguen intactos.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Controllers/RecepcionesHuevoController.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/ \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/ \
  Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/RecepcionesHuevoControllerTests.cs \
  Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoRecepcionesHuevoTests.cs
git commit -m "feat(gestor-caisy): confirmar recepcion de huevo"
```

---

## Cierre SP9C (y de SP9 completo)

- [ ] Ejecutar toda la suite: `dotnet test Icarus/tests/Icarus.UnitTests`,
  `dotnet test Icarus/tests/Icarus.IntegrationTests` (Docker activo),
  `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`,
  `dotnet test Icarus/tests/Icarus.ArchitectureTests`,
  `npm test -- --run` (desde `web/`, sin cambios esperados en la PWA en este
  bloque salvo que Task 5 haya expuesto `/credito` y se quiera mostrar ya en
  la PWA — opcional, fuera del alcance mínimo de este plan).
- [ ] Ejecutar `./verify.ps1`, revisar `git diff --check` y el diff completo
  del bloque.
- [ ] Actualizar el glosario de dominio con "Crédito por despacho",
  "GestorRecepcionHuevos" (si no quedó ya) y el desfase de 14 días.
- [ ] Revisar que `docs/dominio/glosario-avicola.md:137-138` ("los despachos
  de huevos... se definirán en subproyectos futuros") se actualice para
  reflejar que SP9 ya cubre este alcance.
- [ ] Push a `develop` solo cuando las seis tareas estén verdes y los tres
  bloques (SP9A, SP9B, SP9C) estén integrados.
