# SP9D — Corrección y eliminación de publicaciones de precio de huevo — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que `GestorRecepcionHuevos` (1) elimine una publicación de
precio de huevo futura y errónea, y (2) corrija una publicación ya vigente y
errónea que ya fue usada por despachos — con ajuste de crédito para los
despachos ya recibidos y recongelamiento automático de los que aún están en
tránsito.

**Architecture:** Extiende el agregado `PublicacionPrecioHuevo` (SP9A) con un
estado `Corregida` y un enlace hacia la publicación que la reemplaza; agrega
la entidad `AjusteCreditoHuevo` (agregado propio, inmutable) que compensa el
saldo de crédito ya calculado por `RepositorioBalanceCreditoHuevo` (SP9C);
extiende `ConfirmarRecepcionDespachoHuevoHandler` (SP9C) para recongelar
líneas cuya publicación fue corregida antes de llegar a recepción. El caso
"futura y errónea" ya está resuelto por `AnularFutura` (SP9A): este plan solo
le cambia el texto del botón en la UI.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core (SQL Server),
MediatR, FluentValidation, xUnit + NSubstitute, Testcontainers.MsSql,
ASP.NET Core MVC (Trajano.GestorCaisy).

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake, en toda cadena visible,
  comentario y mensaje de error.
- Anti-PII: sin datos biométricos, documentos de identidad ni credenciales en
  el registro de vuelo (Seq) — solo ids técnicos, estados y montos.
- TDD estricto: cada tarea empieza con una prueba en rojo por el motivo
  correcto antes de implementar.
- `./verify.ps1` (o `./verify.sh`) antes de cada commit; prohibido
  `--no-verify`. Docker debe estar activo para integración
  (Testcontainers.MsSql).
- Valores de enums persistidos (`EstadoPublicacionPrecioHuevo`,
  `TipoNotificacionDespachoHuevo`) son estables: agregar al final, nunca
  renumerar.
- Un commit por tarea, con `git diff --check` limpio antes de cada uno.
- Spec de referencia:
  `docs/superpowers/specs/2026-09-10-sp9d-correccion-precio-huevo-design.md`.

---

### Task 1: Dominio — estado Corregida, enlaces y AjusteCreditoHuevo

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoPublicacionPrecioHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PublicacionPrecioHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TipoNotificacionDespachoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInternaDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/AjusteCreditoHuevo.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PublicacionPrecioHuevoTests.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/AjusteCreditoHuevoTests.cs`

**Interfaces:**
- Produces: `EstadoPublicacionPrecioHuevo.Corregida` (=3);
  `PublicacionPrecioHuevo.PublicacionCorrectivaId` (`Guid?`), `.Motivo`
  (`string?`), `.CorregirVigente(Guid publicacionCorrectivaId, string
  motivo)`; `DespachoHuevo.RecongelarLinea(TamanoHuevo tamano, decimal
  precioUnitario, Guid publicacionPrecioHuevoId)`;
  `TipoNotificacionDespachoHuevo.AjusteCredito` (=2);
  `NotificacionInternaDespachoHuevo.ParaAjusteCredito(Guid despachoHuevoId,
  Guid clienteId, string meta)`; `AjusteCreditoHuevo` (ctor `(Guid clienteId,
  Guid despachoHuevoId, Guid publicacionErroneaId, Guid
  publicacionCorrectivaId, decimal monto, string motivo, Guid actorId)`,
  propiedades `ClienteId`, `DespachoHuevoId`, `PublicacionErroneaId`,
  `PublicacionCorrectivaId`, `Monto`, `Motivo`, `ActorId`, `CreadoEnUtc`, `Id`
  heredado de `Entity`).

- [ ] **Step 1: Escribir las pruebas en rojo de `PublicacionPrecioHuevo.CorregirVigente`**

Agregar al final de la clase `PublicacionPrecioHuevoTests` (antes del `}` que
cierra la clase), en
`Icarus/tests/Icarus.UnitTests/GestionAvicola/PublicacionPrecioHuevoTests.cs`:

```csharp
    [Fact]
    public void UnaPublicacionVigenteSePuedeCorregirYQuedaEnlazada()
    {
        var publicacion = BorradorConSeisDetalles();
        publicacion.Publicar();
        var correctivaId = Guid.NewGuid();

        publicacion.CorregirVigente(correctivaId, "Precio al productor cargado con error de digitación.");

        Assert.Equal(EstadoPublicacionPrecioHuevo.Corregida, publicacion.Estado);
        Assert.Equal(correctivaId, publicacion.PublicacionCorrectivaId);
        Assert.Equal("Precio al productor cargado con error de digitación.", publicacion.Motivo);
    }

    [Fact]
    public void SoloUnaPublicadaSePuedeCorregir()
    {
        var borrador = new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m);
        var anulada = BorradorConSeisDetalles();
        anulada.Publicar();
        anulada.AnularFutura(anulada.FechaVigencia.AddDays(-1));
        var yaCorregida = BorradorConSeisDetalles();
        yaCorregida.Publicar();
        yaCorregida.CorregirVigente(Guid.NewGuid(), "primer motivo");

        Assert.Throws<ReglaNegocioException>(() => borrador.CorregirVigente(Guid.NewGuid(), "motivo"));
        Assert.Throws<ReglaNegocioException>(() => anulada.CorregirVigente(Guid.NewGuid(), "motivo"));
        Assert.Throws<ReglaNegocioException>(() => yaCorregida.CorregirVigente(Guid.NewGuid(), "motivo"));
    }

    [Fact]
    public void CorregirExigeUnMotivo()
    {
        var publicacion = BorradorConSeisDetalles();
        publicacion.Publicar();

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            publicacion.CorregirVigente(Guid.NewGuid(), "  "));

        Assert.Equal("La corrección debe declarar un motivo.", excepcion.Message);
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PublicacionPrecioHuevo`
Expected: FAIL — `CS1061` (`CorregirVigente`, `PublicacionCorrectivaId` y
`Motivo` todavía no existen).

- [ ] **Step 2: Agregar el estado `Corregida`**

En `EstadoPublicacionPrecioHuevo.cs`, agregar sin renumerar:

```csharp
// Estados de una publicación de precio de huevo (spec SP9/SP9D), mismo ciclo
// que EstadoNotificacionPreciosAlimentos: Borrador editable, Publicada
// inmutable y vigente hasta que otra publicación posterior entra en vigor,
// Anulada solo alcanza a publicaciones futuras. Corregida es distinta de
// Anulada: una publicación ya vigente que resultó errónea y fue reemplazada,
// con despachos que ya la usaron y que hay que reconciliar (spec SP9D) — a
// diferencia de Anulada, que nunca llegó a regir y no tiene impacto. Valores
// estables.
public enum EstadoPublicacionPrecioHuevo
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
    Corregida = 3,
}
```

- [ ] **Step 3: Implementar `CorregirVigente` en `PublicacionPrecioHuevo`**

En `PublicacionPrecioHuevo.cs`, agregar las dos propiedades junto a
`DocumentoOriginalId` y el método junto a `AnularFutura`:

```csharp
    // Publicación que reemplaza a esta por corrección (spec SP9D); distinto
    // del reemplazo normal por vencimiento, que no se enlaza. Solo se llena
    // vía CorregirVigente.
    public Guid? PublicacionCorrectivaId { get; private set; }

    public string? Motivo { get; private set; }
```

```csharp
    // Corrección de una publicación ya vigente (spec SP9D): a diferencia de
    // AnularFutura, no exige que la vigencia sea futura — el llamador (SP9D,
    // ComandosPreciosHuevo) ya validó que esta es la publicación vigente y
    // que la correctiva no tiene vigencia futura, algo que este método no
    // puede verificar por sí solo porque requiere cargar el otro agregado.
    public void CorregirVigente(Guid publicacionCorrectivaId, string motivo)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Publicada)
            throw new ReglaNegocioException("Solo una publicación vigente se puede corregir.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("La corrección debe declarar un motivo.");
        Estado = EstadoPublicacionPrecioHuevo.Corregida;
        PublicacionCorrectivaId = publicacionCorrectivaId;
        Motivo = motivo;
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PublicacionPrecioHuevo`
Expected: PASS (15/15).

- [ ] **Step 4: Escribir las pruebas en rojo de `DespachoHuevo.RecongelarLinea`**

Agregar al final de la clase `DespachoHuevoTests`, en
`Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs`:

```csharp
    [Fact]
    public void RecongelarLineaSoloAplicaAUnDespachado()
    {
        var despacho = BorradorConDosDetalles();

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.RecongelarLinea(TamanoHuevo.Extra, 0.90m, Guid.NewGuid()));

        Assert.Equal("Solo un despacho despachado permite recongelar un precio.", excepcion.Message);
    }

    [Fact]
    public void RecongelarLineaActualizaPrecioYPublicacion()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId,
            PreciosPara(despacho, Guid.NewGuid()), Documento());
        var publicacionCorrectiva = Guid.NewGuid();

        despacho.RecongelarLinea(TamanoHuevo.Extra, 0.95m, publicacionCorrectiva);

        var linea = despacho.Detalles.Single(d => d.Tamano == TamanoHuevo.Extra);
        Assert.Equal(0.95m, linea.PrecioUnitarioCongelado);
        Assert.Equal(publicacionCorrectiva, linea.PublicacionPrecioHuevoId);
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevoTests`
Expected: FAIL — `CS1061` (`RecongelarLinea` todavía no existe).

- [ ] **Step 5: Implementar `RecongelarLinea` en `DespachoHuevo`**

En `DespachoHuevo.cs`, agregar junto a `ConfirmarRecepcion`:

```csharp
    // Recongela una línea cuando la publicación que la fijó fue corregida
    // antes de que este despacho llegara a recepción (spec SP9D). Solo
    // Despachado: un despacho ya Recibido nunca se recalcula, se compensa
    // con un AjusteCreditoHuevo en su lugar (ver ComandosPreciosHuevo).
    public void RecongelarLinea(TamanoHuevo tamano, decimal precioUnitario, Guid publicacionPrecioHuevoId)
    {
        AsegurarEstado(EstadoDespachoHuevo.Despachado, "Solo un despacho despachado permite recongelar un precio.");
        var linea = _detalles.SingleOrDefault(d => d.Tamano == tamano)
            ?? throw new ReglaNegocioException("El despacho no tiene una línea para ese tamaño.");
        linea.CongelarPrecio(precioUnitario, publicacionPrecioHuevoId);
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevoTests`
Expected: PASS.

- [ ] **Step 6: Agregar `TipoNotificacionDespachoHuevo.AjusteCredito`**

En `TipoNotificacionDespachoHuevo.cs`, agregar sin renumerar (revisar el
comentario existente sobre los valores ya definidos y mantenerlo):

```csharp
public enum TipoNotificacionDespachoHuevo
{
    DespachoRecibido = 0,
    CreditoInsuficiente = 1,
    AjusteCredito = 2,
}
```

- [ ] **Step 7: Agregar `ParaAjusteCredito` a `NotificacionInternaDespachoHuevo`**

En `NotificacionInternaDespachoHuevo.cs`, agregar junto a
`ParaRecepcionConfirmada`:

```csharp
    // Ajuste de crédito por corrección de una publicación vigente (spec
    // SP9D): a diferencia de CreditoInsuficiente, sí tiene DespachoHuevoId
    // (el despacho que originó el ajuste) y ClienteId relleno — es del
    // tenant afectado, no de la bandeja global de CAISY. Meta lleva el monto
    // y el motivo en texto, misma convención que CreditoInsuficiente.
    public static NotificacionInternaDespachoHuevo ParaAjusteCredito(
        Guid despachoHuevoId, Guid clienteId, string meta) =>
        new(TipoNotificacionDespachoHuevo.AjusteCredito, despachoHuevoId, clienteId, meta);
```

- [ ] **Step 8: Escribir las pruebas en rojo de `AjusteCreditoHuevo`**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/AjusteCreditoHuevoTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9D (spec: "Corrección de una publicación vigente"): registro inmutable
// de la diferencia de crédito entre el precio erróneo y el correcto para un
// despacho ya Recibido.
public class AjusteCreditoHuevoTests
{
    [Fact]
    public void SeCreaConLosDatosDeLaCorreccion()
    {
        var clienteId = Guid.NewGuid();
        var despachoId = Guid.NewGuid();
        var erroneaId = Guid.NewGuid();
        var correctivaId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var ajuste = new AjusteCreditoHuevo(
            clienteId, despachoId, erroneaId, correctivaId, 125.50m, "Precio mal digitado.", actorId);

        Assert.Equal(clienteId, ajuste.ClienteId);
        Assert.Equal(despachoId, ajuste.DespachoHuevoId);
        Assert.Equal(erroneaId, ajuste.PublicacionErroneaId);
        Assert.Equal(correctivaId, ajuste.PublicacionCorrectivaId);
        Assert.Equal(125.50m, ajuste.Monto);
        Assert.Equal("Precio mal digitado.", ajuste.Motivo);
        Assert.Equal(actorId, ajuste.ActorId);
    }

    [Fact]
    public void RechazaUnMontoCero()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new AjusteCreditoHuevo(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, "motivo", Guid.NewGuid()));

        Assert.Equal("El monto del ajuste no puede ser cero.", excepcion.Message);
    }

    [Fact]
    public void RechazaUnMotivoVacio()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new AjusteCreditoHuevo(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "  ", Guid.NewGuid()));

        Assert.Equal("El ajuste debe declarar un motivo.", excepcion.Message);
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter AjusteCreditoHuevo`
Expected: FAIL — `CS0246` (`AjusteCreditoHuevo` todavía no existe).

- [ ] **Step 9: Implementar `AjusteCreditoHuevo`**

Crear `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/AjusteCreditoHuevo.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Compensa, en el saldo de crédito del cliente, la diferencia de precio
// entre una publicación errónea y su correctiva para un despacho ya
// Recibido (spec SP9D). Agregado propio (no una sub-entidad de DespachoHuevo)
// porque RepositorioBalanceCreditoHuevo lo suma igual que despachos y
// pedidos, con una consulta que cruza clientes. Inmutable: es un registro
// histórico de una corrección ya aplicada, no algo que se edite.
public sealed class AjusteCreditoHuevo : AggregateRoot
{
    private AjusteCreditoHuevo()
    {
    }

    public AjusteCreditoHuevo(
        Guid clienteId, Guid despachoHuevoId, Guid publicacionErroneaId, Guid publicacionCorrectivaId,
        decimal monto, string motivo, Guid actorId)
    {
        if (monto == 0)
            throw new ReglaNegocioException("El monto del ajuste no puede ser cero.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("El ajuste debe declarar un motivo.");

        ClienteId = clienteId;
        DespachoHuevoId = despachoHuevoId;
        PublicacionErroneaId = publicacionErroneaId;
        PublicacionCorrectivaId = publicacionCorrectivaId;
        Monto = monto;
        Motivo = motivo;
        ActorId = actorId;
        CreadoEnUtc = DateTime.UtcNow;
    }

    public Guid ClienteId { get; private set; }

    public Guid DespachoHuevoId { get; private set; }

    public Guid PublicacionErroneaId { get; private set; }

    public Guid PublicacionCorrectivaId { get; private set; }

    public decimal Monto { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public Guid ActorId { get; private set; }

    public DateTime CreadoEnUtc { get; private set; }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "AjusteCreditoHuevo|PublicacionPrecioHuevo|DespachoHuevoTests"`
Expected: PASS (todas).

- [ ] **Step 10: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoPublicacionPrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PublicacionPrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TipoNotificacionDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInternaDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/AjusteCreditoHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PublicacionPrecioHuevoTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/AjusteCreditoHuevoTests.cs
git commit -m "feat(avicola): modelar correccion de precio de huevo vigente y ajuste de credito"
```

---

### Task 2: Persistencia — tabla, migración, repositorios y balance

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionAjusteCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPublicacionPrecioHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/PuertosPreciosHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioAjustesCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs`
- Migración EF generada por `dotnet ef migrations add`.

**Interfaces:**
- Consumes: `AjusteCreditoHuevo`, `PublicacionPrecioHuevo`, `DespachoHuevo`
  (Task 1).
- Produces: `GestionAvicolaDbContext.AjustesCreditoHuevo` (`DbSet<AjusteCreditoHuevo>`);
  `IRepositorioDespachosHuevo.ListarRecibidosPorPublicacionAsync(Guid
  publicacionId, CancellationToken)`; `IRepositorioAjustesCreditoHuevo.Agregar(AjusteCreditoHuevo)`
  — para que Task 3 los consuma.

- [ ] **Step 1: Configuración EF de `AjusteCreditoHuevo`**

Crear `ConfiguracionAjusteCreditoHuevo.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionAjusteCreditoHuevo : IEntityTypeConfiguration<AjusteCreditoHuevo>
{
    public void Configure(EntityTypeBuilder<AjusteCreditoHuevo> builder)
    {
        builder.ToTable("ajustes_credito_huevo", t =>
            t.HasCheckConstraint("CK_ajustes_credito_huevo_monto", "[Monto] <> 0"));
        // Mismo decimal(18,2) que otros totales agregados del módulo
        // (PedidoAlimento.Recepcion.TotalRecibido, DetallePedidoAlimento.SubtotalSolicitado).
        builder.Property(a => a.Monto).HasColumnType("decimal(18,2)");
        builder.Property(a => a.Motivo).HasMaxLength(500).IsRequired();

        builder.HasIndex(a => a.ClienteId);
        builder.HasIndex(a => a.DespachoHuevoId);
    }
}
```

En `ConfiguracionPublicacionPrecioHuevo.cs`, agregar dentro de `Configure`
(junto a las propiedades existentes):

```csharp
        builder.Property(p => p.Motivo).HasMaxLength(500);
```

- [ ] **Step 2: `DbSet` y filtro de tenant en el DbContext**

En `GestionAvicolaDbContext.cs`, agregar junto al `DbSet` de
`NotificacionesInternasDespachoHuevo`:

```csharp
    // Ajustes de crédito por corrección de precio de huevo (spec SP9D):
    // mismo alcance de tenant que DespachoHuevo — las cuentas sin tenant
    // (CAISY) los ven todos, cada tenant solo los suyos.
    public DbSet<AjusteCreditoHuevo> AjustesCreditoHuevo => Set<AjusteCreditoHuevo>();
```

Y en `OnModelCreating`, junto al filtro de `DespachoHuevo`:

```csharp
        // Ajustes de crédito de huevo (spec SP9D): mismo filtro de tenant
        // que los despachos; sin EstaActivo porque el ajuste es inmutable,
        // nunca se desactiva.
        modelBuilder.Entity<AjusteCreditoHuevo>().HasQueryFilter(a =>
            _clienteIdActual == null || a.ClienteId == _clienteIdActual);
```

- [ ] **Step 3: Nuevo método de consulta en `IRepositorioDespachosHuevo`**

En `PuertosDespachosHuevo.cs`, agregar al final de la interfaz:

```csharp
    // Despachos ya recibidos que congelaron una línea con esta publicación
    // (spec SP9D): usado tanto por la vista previa de una corrección como
    // por el comando que la aplica.
    Task<IReadOnlyList<DespachoHuevo>> ListarRecibidosPorPublicacionAsync(
        Guid publicacionId, CancellationToken cancellationToken = default);
```

En `RepositorioDespachosHuevo.cs`, agregar la implementación:

```csharp
    public async Task<IReadOnlyList<DespachoHuevo>> ListarRecibidosPorPublicacionAsync(
        Guid publicacionId, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo
            .Include(d => d.Detalles)
            .Where(d => d.Estado == EstadoDespachoHuevo.Recibido
                && d.Detalles.Any(det => det.PublicacionPrecioHuevoId == publicacionId))
            .ToListAsync(cancellationToken);
```

- [ ] **Step 4: Puerto e implementación de `IRepositorioAjustesCreditoHuevo`**

En `PuertosPreciosHuevo.cs`, agregar al final del archivo:

```csharp
// Ajustes de crédito generados al corregir una publicación vigente (spec
// SP9D). Solo se agregan, nunca se consultan por id desde este puerto: el
// saldo se lee agregado en RepositorioBalanceCreditoHuevo.
public interface IRepositorioAjustesCreditoHuevo
{
    void Agregar(AjusteCreditoHuevo ajuste);
}
```

Crear `RepositorioAjustesCreditoHuevo.cs`:

```csharp
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioAjustesCreditoHuevo(GestionAvicolaDbContext db) : IRepositorioAjustesCreditoHuevo
{
    public void Agregar(AjusteCreditoHuevo ajuste) => db.AjustesCreditoHuevo.Add(ajuste);
}
```

- [ ] **Step 5: Escribir la prueba de integración en rojo del ajuste en el saldo**

En `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs`, agregar
al final de la clase (antes del `}` de cierre):

```csharp
    [Fact]
    public async Task UnAjusteDeCreditoSumaAlSaldoSinDesfase()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        // FechaRecepcion = hoy: dentro del desfase de 14 días, así que el
        // despacho en sí no aporta nada al saldo (aísla la contribución del
        // ajuste, que no tiene desfase).
        var despacho = DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy());
        var ajuste = new AjusteCreditoHuevo(
            clienteId, despacho.Id, Guid.NewGuid(), Guid.NewGuid(), 150m, "Corrección de precio", actorId);
        await SembrarAsync(despacho, ajuste);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(150m, saldo);
    }
```

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter BalanceCreditoHuevo`
(Docker activo)
Expected: FAIL — `AjusteCreditoHuevo` no se refleja en el saldo (da `0m`).

- [ ] **Step 6: Extender `RepositorioBalanceCreditoHuevo`**

En `RepositorioBalanceCreditoHuevo.cs`, agregar antes del `return` final:

```csharp
        // Ajustes de corrección (spec SP9D): compensan un error real, no un
        // ingreso sujeto al desfase de dos semanas de ReglasCreditoHuevo.
        var ajustes = await db.AjustesCreditoHuevo
            .Where(a => a.ClienteId == clienteId)
            .SumAsync(a => a.Monto, cancellationToken);
```

Y cambiar el `return` de:

```csharp
        return ingresos - recibidoReal - comprometidoPendiente;
```

a:

```csharp
        return ingresos - recibidoReal - comprometidoPendiente + ajustes;
```

- [ ] **Step 7: Generar la migración**

```bash
dotnet ef migrations add CorreccionPrecioHuevo --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host
```

Verificar que la migración crea la tabla `ajustes_credito_huevo` con el check
constraint y los índices esperados, y agrega las columnas
`PublicacionCorrectivaId` (nullable) y `Motivo` (nvarchar(500) nullable) a
`publicaciones_precios_huevo`.

- [ ] **Step 8: Registrar el nuevo repositorio en DI**

En `DependencyInjection.cs`, agregar junto al registro de
`IRepositorioDespachosHuevo`:

```csharp
        servicios.AddScoped<IRepositorioAjustesCreditoHuevo, RepositorioAjustesCreditoHuevo>();
```

- [ ] **Step 9: Correr la suite de integración completa (Docker activo)**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS — confirma que la migración aplica limpia y
`UnAjusteDeCreditoSumaAlSaldoSinDesfase` pasa junto con el resto de
`BalanceCreditoHuevoTests`.

- [ ] **Step 10: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionAjusteCreditoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPublicacionPrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/PuertosPreciosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioAjustesCreditoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/
git commit -m "feat(avicola): persistir ajustes de credito de huevo y reflejarlos en el saldo"
```

---

### Task 3: Aplicación — Previsualizar y Corregir, más reconciliación en ConfirmarRecepcion

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ConfirmarRecepcionDespachoHuevoHandlerTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `AjusteCreditoHuevo`, `IRepositorioAjustesCreditoHuevo`,
  `IRepositorioDespachosHuevo.ListarRecibidosPorPublicacionAsync`,
  `PublicacionPrecioHuevo.CorregirVigente`, `DespachoHuevo.RecongelarLinea`
  (Tasks 1-2).
- Produces: `PrevisualizarCorreccionPrecioHuevoQuery(Guid
  PublicacionErroneaId, Guid PublicacionCorrectivaId) : IRequest<VistaPreviaCorreccionPrecioHuevo>`;
  `VistaPreviaCorreccionPrecioHuevo(IReadOnlyList<AjusteCorreccionPrecioHuevoResumen>
  Ajustes, decimal Total)`; `AjusteCorreccionPrecioHuevoResumen(Guid
  DespachoHuevoId, DateOnly? FechaRecepcion, decimal Monto)`;
  `CorregirPublicacionPrecioHuevoVigenteCommand(Guid PublicacionErroneaId,
  Guid PublicacionCorrectivaId, string Motivo) : IRequest` — para que Task 4
  los exponga por HTTP.

- [ ] **Step 1: Escribir las pruebas en rojo de `CorregirPublicacionPrecioHuevoVigenteHandler`**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9D (spec: "Corrección de una publicación vigente"): la vista previa no
// persiste nada; confirmar publica la correctiva, corrige la errónea y
// genera un ajuste + notificación por cada despacho Recibido con diferencia
// distinta de cero. Los despachos Despachado no se tocan acá — su
// reconciliación es perezosa, en ConfirmarRecepcion.
public class CorregirPublicacionPrecioHuevoHandlerTests
{
    private static readonly DateOnly FechaNotificacion = FechasNegocio.Hoy().AddDays(-15);
    private static readonly DateOnly FechaVigencia = FechasNegocio.Hoy().AddDays(-10);

    private readonly IRepositorioPublicacionesPreciosHuevo _repositorioPrecios =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
    private readonly IRepositorioDespachosHuevo _repositorioDespachos =
        Substitute.For<IRepositorioDespachosHuevo>();
    private readonly IRepositorioAjustesCreditoHuevo _repositorioAjustes =
        Substitute.For<IRepositorioAjustesCreditoHuevo>();
    private readonly INotificacionesInternasDespachoHuevo _notificaciones =
        Substitute.For<INotificacionesInternasDespachoHuevo>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    public CorregirPublicacionPrecioHuevoHandlerTests() =>
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());

    private CorregirPublicacionPrecioHuevoVigenteHandler CrearHandler() => new(
        _repositorioPrecios, _repositorioDespachos, _repositorioAjustes, _notificaciones,
        _usuarioActual, _registroVuelo, _unidadTrabajo);

    private static PublicacionPrecioHuevo PublicacionVigente(decimal precioExtra, decimal servicio = 0.05m)
    {
        var publicacion = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, servicio, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, precioExtra)]);
        publicacion.Publicar();
        return publicacion;
    }

    private static DespachoHuevo DespachoRecibidoQueUso(Guid publicacionId, Guid clienteId)
    {
        var despacho = new DespachoHuevo(clienteId, Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        despacho.Despachar(FechaVigencia, Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Extra, 0.75m, publicacionId)],
            new DatosDocumentoNota(Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash", "nota.jpg"));
        despacho.ConfirmarRecepcion(FechaVigencia.AddDays(1), Guid.NewGuid());
        return despacho;
    }

    [Fact]
    public async Task ConfirmarCorrigeLaErroneaPublicaLaCorrectivaYAjustaLosRecibidos()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        var clienteId = Guid.NewGuid();
        var despacho = DespachoRecibidoQueUso(erronea.Id, clienteId);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        await CrearHandler().Handle(
            new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "Precio mal digitado."),
            CancellationToken.None);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Corregida, erronea.Estado);
        Assert.Equal(correctiva.Id, erronea.PublicacionCorrectivaId);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, correctiva.Estado);
        // Diferencia: (0.85 + 0.05) - 0.75 congelado = 0.15 por huevo, y la
        // línea es 1 amarra = 180 huevos (DetalleDespachoHuevo.HuevosPorAmarra):
        // 0.15 * 180 = 27.
        _repositorioAjustes.Received(1).Agregar(Arg.Is<AjusteCreditoHuevo>(a =>
            a.ClienteId == clienteId && a.DespachoHuevoId == despacho.Id && a.Monto == 27m));
        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInternaDespachoHuevo>(n =>
            n.Tipo == TipoNotificacionDespachoHuevo.AjusteCredito &&
            n.DespachoHuevoId == despacho.Id && n.ClienteId == clienteId));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnDespachoSinDiferenciaNoGeneraAjuste()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.70m)]);
        var despacho = DespachoRecibidoQueUso(erronea.Id, Guid.NewGuid());
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        await CrearHandler().Handle(
            new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "Revisión sin cambios."),
            CancellationToken.None);

        _repositorioAjustes.DidNotReceive().Agregar(Arg.Any<AjusteCreditoHuevo>());
        _notificaciones.DidNotReceive().Agregar(Arg.Any<NotificacionInternaDespachoHuevo>());
    }

    [Fact]
    public async Task LaCorrectivaConVigenciaFuturaSeRechaza()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechasNegocio.Hoy().AddDays(5), 0.05m,
            [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            CrearHandler().Handle(
                new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "motivo"),
                CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SoloSePuedeCorregirLaPublicacionRealmenteVigente()
    {
        var erronea = PublicacionVigente(0.75m);
        var otraVigente = PublicacionVigente(0.60m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        // La vigente real es otra distinta de "erronea" (por ejemplo, una
        // publicación histórica ya superada, no la efectiva actual).
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(otraVigente);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearHandler().Handle(
                new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "motivo"),
                CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter CorregirPublicacionPrecioHuevo`
Expected: FAIL — `CS0246` (`CorregirPublicacionPrecioHuevoVigenteHandler` y
`CorregirPublicacionPrecioHuevoVigenteCommand` todavía no existen).

- [ ] **Step 2: Implementar la vista previa y el comando de corrección**

En `ComandosPreciosHuevo.cs`, agregar a los `using` del principio:

```csharp
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
```

Y agregar al final del archivo:

```csharp
public sealed record PrevisualizarCorreccionPrecioHuevoQuery(Guid PublicacionErroneaId, Guid PublicacionCorrectivaId)
    : IRequest<VistaPreviaCorreccionPrecioHuevo>;

public sealed record AjusteCorreccionPrecioHuevoResumen(Guid DespachoHuevoId, DateOnly? FechaRecepcion, decimal Monto);

public sealed record VistaPreviaCorreccionPrecioHuevo(
    IReadOnlyList<AjusteCorreccionPrecioHuevoResumen> Ajustes, decimal Total);

public sealed record CorregirPublicacionPrecioHuevoVigenteCommand(
    Guid PublicacionErroneaId, Guid PublicacionCorrectivaId, string Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.corregir-vigente",
        new Dictionary<string, DatoRegistroVuelo> { ["DespachosAjustados"] = DatoRegistroVuelo.Entero });
}

public sealed class CorregirPublicacionPrecioHuevoVigenteValidator
    : AbstractValidator<CorregirPublicacionPrecioHuevoVigenteCommand>
{
    public CorregirPublicacionPrecioHuevoVigenteValidator()
    {
        RuleFor(c => c.PublicacionErroneaId).NotEmpty();
        RuleFor(c => c.PublicacionCorrectivaId).NotEmpty();
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}

// Vista previa (spec SP9D): solo lectura, no publica ni corrige nada — el
// gestor la usa para decidir si confirma. Calcula la diferencia con los
// precios de la correctiva tal cual está (aunque todavía sea un borrador).
public sealed class PrevisualizarCorreccionPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IRepositorioDespachosHuevo repositorioDespachos)
    : IRequestHandler<PrevisualizarCorreccionPrecioHuevoQuery, VistaPreviaCorreccionPrecioHuevo>
{
    public async Task<VistaPreviaCorreccionPrecioHuevo> Handle(
        PrevisualizarCorreccionPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var erronea = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionErroneaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionErroneaId);
        var correctiva = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionCorrectivaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionCorrectivaId);

        var despachos = await repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, cancellationToken);
        var ajustes = despachos
            .Select(d => new AjusteCorreccionPrecioHuevoResumen(
                d.Id, d.FechaRecepcion, CalcularDiferencia(d, erronea, correctiva)))
            .Where(a => a.Monto != 0)
            .ToList();
        return new VistaPreviaCorreccionPrecioHuevo(ajustes, ajustes.Sum(a => a.Monto));
    }

    // Compartido con CorregirPublicacionPrecioHuevoVigenteHandler para que la
    // vista previa y la aplicación real calculen exactamente lo mismo.
    internal static decimal CalcularDiferencia(
        DespachoHuevo despacho, PublicacionPrecioHuevo erronea, PublicacionPrecioHuevo correctiva)
    {
        var preciosCorrectivos = correctiva.Detalles
            .ToDictionary(d => d.Tamano, d => d.PrecioAlProductor + correctiva.Servicio);
        var total = 0m;
        foreach (var linea in despacho.Detalles)
        {
            if (linea.PublicacionPrecioHuevoId != erronea.Id) continue;
            if (!preciosCorrectivos.TryGetValue(linea.Tamano, out var precioCorrecto)) continue;
            var cantidad = linea.CantidadAmarras * DetalleDespachoHuevo.HuevosPorAmarra + linea.UnidadesSueltas;
            total += (precioCorrecto - (linea.PrecioUnitarioCongelado ?? 0m)) * cantidad;
        }
        return total;
    }
}

// Corrección (spec SP9D): publica la correctiva, corrige la errónea y
// aplica un ajuste + notificación por cada despacho Recibido con diferencia
// distinta de cero. Solo se puede corregir la publicación que está
// realmente vigente hoy — no cualquier Publicada del historial — para no
// reconciliar despachos que ya estaban correctamente valorados a su propia
// fecha.
public sealed class CorregirPublicacionPrecioHuevoVigenteHandler(
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IRepositorioDespachosHuevo repositorioDespachos,
    IRepositorioAjustesCreditoHuevo repositorioAjustes,
    INotificacionesInternasDespachoHuevo notificaciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CorregirPublicacionPrecioHuevoVigenteCommand>
{
    public async Task Handle(CorregirPublicacionPrecioHuevoVigenteCommand request, CancellationToken cancellationToken)
    {
        var erronea = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionErroneaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionErroneaId);
        var correctiva = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionCorrectivaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionCorrectivaId);
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        var vigenteActual = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken);
        if (vigenteActual is null || vigenteActual.Id != erronea.Id)
            throw new ConflictException("Solo se puede corregir la publicación vigente.");
        if (correctiva.FechaVigencia > hoy)
            throw new ValidationException("La publicación correctiva no puede tener vigencia futura.");
        if (await repositorioPrecios.ExistePublicadaConVigenciaIgualAsync(
                correctiva.FechaVigencia, correctiva.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");

        var despachos = await repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, cancellationToken);

        correctiva.Publicar();
        erronea.CorregirVigente(correctiva.Id, request.Motivo);

        var ajustados = 0;
        foreach (var despacho in despachos)
        {
            var monto = PrevisualizarCorreccionPrecioHuevoHandler.CalcularDiferencia(despacho, erronea, correctiva);
            if (monto == 0) continue;
            var ajuste = new AjusteCreditoHuevo(
                despacho.ClienteId, despacho.Id, erronea.Id, correctiva.Id, monto, request.Motivo, actorId);
            repositorioAjustes.Agregar(ajuste);
            notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaAjusteCredito(
                despacho.Id, despacho.ClienteId,
                FormattableString.Invariant($"{monto:0.00} Bs — {request.Motivo}")));
            ajustados++;
        }

        registroVuelo.Decidir("avicola.precios-huevo.corregir-vigente", "correccion", "aplicada",
            new Dictionary<string, object?> { ["DespachosAjustados"] = ajustados });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter CorregirPublicacionPrecioHuevo`
Expected: PASS (4/4).

- [ ] **Step 3: Escribir la prueba en rojo de la reconciliación perezosa**

En `ConfirmarRecepcionDespachoHuevoHandlerTests.cs`, agregar el campo y
actualizar el constructor del handler:

```csharp
    private readonly IRepositorioPublicacionesPreciosHuevo _repositorioPrecios =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
```

```csharp
    private ConfirmarRecepcionDespachoHuevoHandler CrearHandler() =>
        new(_repositorio, _repositorioPrecios, _usuarioActual, _registroVuelo, _unidadTrabajo, _notificaciones);
```

Agregar `using Icarus.GestionAvicola.Application.PreciosHuevo;` a los `using`
del archivo, y agregar al final de la clase (antes del `}` de cierre):

```csharp
    [Fact]
    public async Task RecibirUnDespachoCuyaPublicacionFueCorregidaRecongelaAntesDeConfirmar()
    {
        var publicacionOriginal = new PublicacionPrecioHuevo(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), 0.05m,
            [new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 0.70m)]);
        publicacionOriginal.Publicar();
        var publicacionCorrectiva = new PublicacionPrecioHuevo(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), 0.05m,
            [new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 0.90m)]);
        publicacionCorrectiva.Publicar();
        publicacionOriginal.CorregirVigente(publicacionCorrectiva.Id, "Precio mal digitado.");

        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 2, 30)]);
        despacho.Despachar(FechasNegocio.Hoy(), Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Primera, 0.75m, publicacionOriginal.Id)],
            new DatosDocumentoNota(Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "nota.jpg"));
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);
        _repositorioPrecios.ObtenerPorIdAsync(publicacionOriginal.Id, Arg.Any<CancellationToken>())
            .Returns(publicacionOriginal);
        _repositorioPrecios.ObtenerPorIdAsync(publicacionCorrectiva.Id, Arg.Any<CancellationToken>())
            .Returns(publicacionCorrectiva);

        await CrearHandler().Handle(
            new ConfirmarRecepcionDespachoHuevoCommand(despacho.Id), CancellationToken.None);

        var linea = despacho.Detalles.Single();
        Assert.Equal(0.95m, linea.PrecioUnitarioCongelado);
        Assert.Equal(publicacionCorrectiva.Id, linea.PublicacionPrecioHuevoId);
        Assert.Equal(EstadoDespachoHuevo.Recibido, despacho.Estado);
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ConfirmarRecepcionDespachoHuevoHandler`
Expected: FAIL — `CS7036` (el constructor del handler todavía no acepta
`IRepositorioPublicacionesPreciosHuevo`).

- [ ] **Step 4: Extender `ConfirmarRecepcionDespachoHuevoHandler` con la reconciliación**

En `ComandosDespachosHuevo.cs`, reemplazar la clase
`ConfirmarRecepcionDespachoHuevoHandler` completa por:

```csharp
public sealed class ConfirmarRecepcionDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
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

        await ReconciliarPreciosCorregidosAsync(despacho, cancellationToken);

        despacho.ConfirmarRecepcion(FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            despacho.Id, despacho.ClienteId));
        registroVuelo.Decidir("avicola.despachos-huevo.confirmar-recepcion", "recepcion", "aplicada",
            new Dictionary<string, object?> { ["TotalBs"] = despacho.TotalBs });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // Reconciliación perezosa (spec SP9D): si la publicación que congeló una
    // línea fue corregida mientras el despacho seguía en tránsito, la línea
    // se recongela contra la publicación activa al final de la cadena de
    // correcciones, antes de sellar la recepción. Un despacho ya Recibido
    // nunca pasa por acá — su camino de corrección es AjusteCreditoHuevo.
    private async Task ReconciliarPreciosCorregidosAsync(DespachoHuevo despacho, CancellationToken cancellationToken)
    {
        foreach (var linea in despacho.Detalles)
        {
            if (linea.PublicacionPrecioHuevoId is not { } publicacionId)
                continue;
            var idOriginal = publicacionId;
            var publicacion = await repositorioPrecios.ObtenerPorIdAsync(publicacionId, cancellationToken);
            while (publicacion is not null
                && publicacion.Estado == EstadoPublicacionPrecioHuevo.Corregida
                && publicacion.PublicacionCorrectivaId is { } siguienteId)
            {
                publicacion = await repositorioPrecios.ObtenerPorIdAsync(siguienteId, cancellationToken);
            }
            if (publicacion is null || publicacion.Id == idOriginal)
                continue;
            var precio = publicacion.Detalles.SingleOrDefault(d => d.Tamano == linea.Tamano);
            if (precio is null)
                continue;
            despacho.RecongelarLinea(linea.Tamano, precio.PrecioAlProductor + publicacion.Servicio, publicacion.Id);
        }
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ConfirmarRecepcionDespachoHuevoHandler`
Expected: PASS (4/4 — las tres pruebas ya existentes de SP9C siguen pasando
porque `_repositorioPrecios` sin stub devuelve `null` en `ObtenerPorIdAsync`,
así que el bucle no encuentra nada que recongelar).

- [ ] **Step 5: Correr toda la suite de Application y Domain**

Run: `dotnet test Icarus/tests/Icarus.UnitTests`
Expected: PASS (todas).

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/ConfirmarRecepcionDespachoHuevoHandlerTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/CorregirPublicacionPrecioHuevoHandlerTests.cs
git commit -m "feat(avicola): comando corregir precio de huevo vigente y reconciliacion en recepcion"
```

---

### Task 4: Host — endpoints de previsualización y corrección

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/PreciosHuevoEndpoints.cs`

**Interfaces:**
- Consumes: `PrevisualizarCorreccionPrecioHuevoQuery`,
  `CorregirPublicacionPrecioHuevoVigenteCommand` (Task 3).
- Produces: `GET /precios-huevo-caisy/corregir/previsualizar?erronea={id}&correctiva={id}`,
  `POST /precios-huevo-caisy/corregir` — para que Task 5 los consuma desde
  Trajano.GestorCaisy.

- [ ] **Step 1: Agregar las rutas**

En `PreciosHuevoEndpoints.cs`, agregar las dos rutas nuevas antes de
`/{id:guid}` (mismo criterio ya usado para `/vigente`, que tampoco es un
Guid válido y por eso no compite con esa ruta):

```csharp
        grupo.MapGet("/corregir/previsualizar", async (Guid erronea, Guid correctiva,
            ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(
                new PrevisualizarCorreccionPrecioHuevoQuery(erronea, correctiva), cancellationToken)));

        grupo.MapPost("/corregir", async (CorregirPublicacionPrecioHuevoVigenteCommand comando,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(comando, cancellationToken);
            return Results.NoContent();
        });

```

(Insertar el bloque justo antes de la línea `grupo.MapGet("/{id:guid}", ...)`.)

- [ ] **Step 2: Compilar y correr las pruebas de arquitectura**

Run: `dotnet build Icarus/Icarus.sln`
Expected: build correcto.

Run: `dotnet test Icarus/tests/Icarus.ArchitectureTests`
Expected: PASS (las reglas de capas no se ven afectadas: el endpoint solo usa
MediatR, igual que el resto del archivo).

- [ ] **Step 3: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PreciosHuevoEndpoints.cs
git commit -m "feat(api): exponer previsualizacion y correccion de precio de huevo vigente"
```

---

### Task 5: Trajano.GestorCaisy — contratos y cliente HTTP

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`

**Interfaces:**
- Consumes: endpoints de Task 4.
- Produces: `IApiIcarusClient.ObtenerPublicacionVigenteHuevoAsync`,
  `.PrevisualizarCorreccionHuevoAsync`, `.CorregirVigenteHuevoAsync`; DTOs
  `AjusteCorreccionHuevoResumenApi`, `VistaPreviaCorreccionHuevoApi`,
  `ComandoCorregirVigenteHuevoApi` — para que Task 6 construya el
  controlador y las vistas.

- [ ] **Step 1: Agregar los DTOs**

En `ContratosApi.cs`, agregar junto a `ComandoActualizarBorradorHuevoApi`:

```csharp
public sealed record AjusteCorreccionHuevoResumenApi(Guid DespachoHuevoId, DateOnly? FechaRecepcion, decimal Monto);

public sealed record VistaPreviaCorreccionHuevoApi(
    IReadOnlyList<AjusteCorreccionHuevoResumenApi> Ajustes, decimal Total);

public sealed record ComandoCorregirVigenteHuevoApi(Guid PublicacionErroneaId, Guid PublicacionCorrectivaId, string Motivo);
```

- [ ] **Step 2: Agregar los métodos a la interfaz**

En `IApiIcarusClient.cs`, agregar junto a
`DescargarDocumentoOriginalHuevoAsync`:

```csharp
    Task<PublicacionPrecioHuevoDetalleApi?> ObtenerPublicacionVigenteHuevoAsync(CancellationToken token = default);

    Task<VistaPreviaCorreccionHuevoApi> PrevisualizarCorreccionHuevoAsync(
        Guid erroneaId, Guid correctivaId, CancellationToken token = default);

    Task CorregirVigenteHuevoAsync(
        Guid erroneaId, Guid correctivaId, string motivo, CancellationToken token = default);
```

- [ ] **Step 3: Escribir la prueba en rojo del cliente falso**

En `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`, agregar
junto a los campos de huevo existentes (cerca de `ErrorDeDescargarHuevo`):

```csharp
    public Exception? ErrorDeVigenteHuevo { get; set; }
    public Exception? ErrorDePrevisualizarCorreccionHuevo { get; set; }
    public Exception? ErrorDeCorregirHuevo { get; set; }

    public PublicacionPrecioHuevoDetalleApi? VigenteHuevo { get; set; }
    public VistaPreviaCorreccionHuevoApi PreviaCorreccionHuevo { get; set; } = new([], 0m);

    public int VecesObtenerVigenteHuevo { get; private set; }
    public int VecesPrevisualizarCorreccionHuevo { get; private set; }
    public int VecesCorregirHuevo { get; private set; }

    public (Guid Erronea, Guid Correctiva)? UltimaPrevisualizacionHuevo { get; private set; }
    public ComandoCorregirVigenteHuevoApi? UltimoComandoCorregirHuevo { get; private set; }
```

Y, junto a `DescargarDocumentoOriginalHuevoAsync`, agregar la implementación
de los tres métodos nuevos de la interfaz:

```csharp
    public Task<PublicacionPrecioHuevoDetalleApi?> ObtenerPublicacionVigenteHuevoAsync(
        CancellationToken token = default)
    {
        VecesObtenerVigenteHuevo++;
        if (ErrorDeVigenteHuevo is not null) throw ErrorDeVigenteHuevo;
        return Task.FromResult(VigenteHuevo);
    }

    public Task<VistaPreviaCorreccionHuevoApi> PrevisualizarCorreccionHuevoAsync(
        Guid erroneaId, Guid correctivaId, CancellationToken token = default)
    {
        VecesPrevisualizarCorreccionHuevo++;
        UltimaPrevisualizacionHuevo = (erroneaId, correctivaId);
        if (ErrorDePrevisualizarCorreccionHuevo is not null) throw ErrorDePrevisualizarCorreccionHuevo;
        return Task.FromResult(PreviaCorreccionHuevo);
    }

    public Task CorregirVigenteHuevoAsync(
        Guid erroneaId, Guid correctivaId, string motivo, CancellationToken token = default)
    {
        VecesCorregirHuevo++;
        UltimoComandoCorregirHuevo = new ComandoCorregirVigenteHuevoApi(erroneaId, correctivaId, motivo);
        if (ErrorDeCorregirHuevo is not null) throw ErrorDeCorregirHuevo;
        return Task.CompletedTask;
    }
```

Run: `dotnet build Icarus/Icarus.sln`
Expected: FAIL — `ApiIcarusClient` todavía no implementa los tres métodos
nuevos de `IApiIcarusClient` (el proyecto de producción no compila).

- [ ] **Step 4: Implementar los métodos en `ApiIcarusClient`**

En `ApiIcarusClient.cs`, agregar junto a `DescargarDocumentoOriginalHuevoAsync`:

```csharp
    public async Task<PublicacionPrecioHuevoDetalleApi?> ObtenerPublicacionVigenteHuevoAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, "precios-huevo-caisy/vigente", accessToken), token);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PublicacionPrecioHuevoDetalleApi>(Json, token);
    }

    public async Task<VistaPreviaCorreccionHuevoApi> PrevisualizarCorreccionHuevoAsync(
        Guid erroneaId, Guid correctivaId, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"precios-huevo-caisy/corregir/previsualizar?erronea={erroneaId}&correctiva={correctivaId}",
                accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<VistaPreviaCorreccionHuevoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task CorregirVigenteHuevoAsync(
        Guid erroneaId, Guid correctivaId, string motivo, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post, "precios-huevo-caisy/corregir", accessToken,
                new ComandoCorregirVigenteHuevoApi(erroneaId, correctivaId, motivo)), token);
        await AsegurarExitoAsync(respuesta, token);
    }
```

Run: `dotnet build Icarus/Icarus.sln`
Expected: build correcto.

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS (todas las pruebas existentes siguen verdes; no hay pruebas
nuevas en esta tarea, las agrega Task 6 al usar el cliente falso).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs \
  Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs
git commit -m "feat(gestor-caisy): cliente http para corregir precio de huevo vigente"
```

---

### Task 6: Trajano.GestorCaisy — pantalla de corrección y renombrar Anular a Eliminar

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosHuevoVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Corregir.cshtml`
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs`

**Interfaces:**
- Consumes: `IApiIcarusClient.ObtenerPublicacionVigenteHuevoAsync`,
  `.PrevisualizarCorreccionHuevoAsync`, `.CorregirVigenteHuevoAsync` (Task 5).

- [ ] **Step 1: Modelos de vista**

En `PreciosHuevoVistas.cs`, agregar al final del archivo:

```csharp
public sealed record VistaCorregirHuevo(
    PublicacionPrecioHuevoDetalleApi Vigente,
    PublicacionPrecioHuevoDetalleApi Correctiva,
    VistaPreviaCorreccionHuevoApi Previa,
    FormularioCorregirHuevoVista Formulario);

public sealed class FormularioCorregirHuevoVista
{
    public Guid CorrectivaId { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
```

Agregar `using System.ComponentModel.DataAnnotations;` a los `using` del
archivo si todavía no está (verificar contra `PedidosVistas.cs`, que ya usa
`[Required]`/`[StringLength]` con ese mismo using).

- [ ] **Step 2: Escribir las pruebas en rojo del controlador**

En `PreciosHuevoControllerTests.cs`, agregar al final de la clase (antes del
`}` de cierre, pero antes de los métodos privados `CrearArchivo`/`UsuarioGestorcaisy`):

```csharp
    [Fact]
    public async Task CorregirMuestraLaVistaPreviaContraLaVigente()
    {
        var vigenteId = Guid.NewGuid();
        var correctivaId = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(correctivaId, "Borrador");
        _api.VigenteHuevo = ApiIcarusFalsa.CrearDetalleHuevo(vigenteId, "Publicada", fechaVigencia: "2025-01-01");
        _api.PreviaCorreccionHuevo = new VistaPreviaCorreccionHuevoApi(
            [new AjusteCorreccionHuevoResumenApi(Guid.NewGuid(), new DateOnly(2025, 11, 10), 12.50m)], 12.50m);

        var vista = await _controlador.Corregir(correctivaId, default);

        var modelo = Assert.IsType<VistaCorregirHuevo>(((ViewResult)vista).Model);
        Assert.Equal(vigenteId, modelo.Vigente.Id);
        Assert.Equal(correctivaId, modelo.Correctiva.Id);
        Assert.Equal(12.50m, modelo.Previa.Total);
        Assert.Equal(1, _api.VecesPrevisualizarCorreccionHuevo);
    }

    [Fact]
    public async Task CorregirDeUnaNoBorradorRedirigeADetalles()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Publicada");

        var resultado = await _controlador.Corregir(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task CorregirSinPublicacionVigenteRedirigeADetalles()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");
        _api.VigenteHuevo = null;

        var resultado = await _controlador.Corregir(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task ConfirmarCorregirInvocaAlClienteYRedirigeConExito()
    {
        var id = Guid.NewGuid();
        var vigenteId = Guid.NewGuid();
        _api.VigenteHuevo = ApiIcarusFalsa.CrearDetalleHuevo(vigenteId, "Publicada", fechaVigencia: "2025-01-01");

        var resultado = await _controlador.Corregir(
            id, new FormularioCorregirHuevoVista { CorrectivaId = id, Motivo = "Precio mal digitado." }, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
        Assert.Equal(
            new ComandoCorregirVigenteHuevoApi(vigenteId, id, "Precio mal digitado."),
            _api.UltimoComandoCorregirHuevo);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task ConfirmarCorregirConErrorDeNegocioRegresaAlFormularioConElMensaje()
    {
        var id = Guid.NewGuid();
        var vigenteId = Guid.NewGuid();
        _api.VigenteHuevo = ApiIcarusFalsa.CrearDetalleHuevo(vigenteId, "Publicada", fechaVigencia: "2025-01-01");
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");
        _api.ErrorDeCorregirHuevo = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Corregir(
            id, new FormularioCorregirHuevoVista { CorrectivaId = id, Motivo = "Precio mal digitado." }, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Corregir), redireccion.ActionName);
        Assert.Contains("Conflicto", _controlador.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task ConfirmarCorregirSinMotivoReconstruyeLaVistaPrevia()
    {
        var id = Guid.NewGuid();
        var vigenteId = Guid.NewGuid();
        _api.VigenteHuevo = ApiIcarusFalsa.CrearDetalleHuevo(vigenteId, "Publicada", fechaVigencia: "2025-01-01");
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");
        _controlador.ModelState.AddModelError("Motivo", "El motivo es obligatorio.");

        var resultado = await _controlador.Corregir(
            id, new FormularioCorregirHuevoVista { CorrectivaId = id, Motivo = string.Empty }, default);

        var modelo = Assert.IsType<VistaCorregirHuevo>(((ViewResult)resultado).Model);
        Assert.Equal(id, modelo.Correctiva.Id);
        Assert.Equal(0, _api.VecesCorregirHuevo);
    }
```

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter PreciosHuevoControllerTests`
Expected: FAIL — `CS1061` (`PreciosHuevoController.Corregir` todavía no
existe).

- [ ] **Step 3: Implementar las acciones del controlador**

En `PreciosHuevoController.cs`, agregar junto a la acción `Anular`:

```csharp
    [HttpGet("{id:guid}/Corregir")]
    public async Task<IActionResult> Corregir(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var correctiva = await api.ObtenerPublicacionHuevoAsync(id, token);
        if (correctiva.Estado != "Borrador")
            return RedirectToAction(nameof(Detalles), new { id });
        var vigente = await api.ObtenerPublicacionVigenteHuevoAsync(token);
        if (vigente is null)
            return RedirectToAction(nameof(Detalles), new { id });
        var previa = await api.PrevisualizarCorreccionHuevoAsync(vigente.Id, id, token);
        return View(new VistaCorregirHuevo(vigente, correctiva, previa, new FormularioCorregirHuevoVista { CorrectivaId = id }));
    }

    [HttpPost("{id:guid}/Corregir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Corregir(Guid id, FormularioCorregirHuevoVista formulario, CancellationToken token)
    {
        formulario.CorrectivaId = id;
        var vigente = await api.ObtenerPublicacionVigenteHuevoAsync(token);
        if (vigente is null)
            return RedirectToAction(nameof(Detalles), new { id });
        if (!ModelState.IsValid)
        {
            var correctivaInvalida = await api.ObtenerPublicacionHuevoAsync(id, token);
            var previaInvalida = await api.PrevisualizarCorreccionHuevoAsync(vigente.Id, id, token);
            return View(new VistaCorregirHuevo(vigente, correctivaInvalida, previaInvalida, formulario));
        }
        try
        {
            await api.CorregirVigenteHuevoAsync(vigente.Id, id, formulario.Motivo, token);
            TempData["Exito"] = "La publicación se corrigió y los ajustes de crédito quedaron aplicados.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(Corregir), new { id });
        }
    }
```

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter PreciosHuevoControllerTests`
Expected: PASS (todas, incluidas las ya existentes).

- [ ] **Step 4: Vista `Corregir.cshtml`**

Crear `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Corregir.cshtml`:

```cshtml
@model VistaCorregirHuevo
@{
    ViewData["Titulo"] = "Corregir publicación de precio de huevo";
    ViewData["Seccion"] = "precios-huevo";
}
<div class="pagina-cabecera">
    <div>
        <h1>Corregir publicación de precio de huevo</h1>
        <p class="pagina-cabecera__detalle">
            La publicación vigente desde el @Model.Vigente.FechaVigencia.ToString("dd/MM/yyyy")
            quedará reemplazada por este borrador. Los despachos ya recibidos que usaron el
            precio erróneo reciben un ajuste de crédito; los que todavía están en tránsito se
            recongelan automáticamente al confirmarse su recepción.
        </p>
    </div>
</div>
<partial name="_Avisos" />
<section class="tarjeta tarjeta--tabla">
    <h2>Despachos ya recibidos que se ajustarán</h2>
    @if (Model.Previa.Ajustes.Count == 0)
    {
        <p class="vacio__texto">Ningún despacho recibido usó el precio erróneo: no hay ajustes que aplicar.</p>
    }
    else
    {
        <table class="tabla">
            <thead>
                <tr>
                    <th scope="col">Despacho</th>
                    <th scope="col">Fecha de recepción</th>
                    <th scope="col">Ajuste (Bs)</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var ajuste in Model.Previa.Ajustes)
                {
                    <tr>
                        <td>@ajuste.DespachoHuevoId</td>
                        <td>@(ajuste.FechaRecepcion?.ToString("dd/MM/yyyy") ?? "—")</td>
                        <td>@ajuste.Monto.ToString("0.00")</td>
                    </tr>
                }
            </tbody>
            <tfoot>
                <tr>
                    <td colspan="2">Total</td>
                    <td>@Model.Previa.Total.ToString("0.00")</td>
                </tr>
            </tfoot>
        </table>
    }
</section>
<section class="tarjeta">
    <form asp-action="Corregir" asp-route-id="@Model.Correctiva.Id" method="post">
        <div class="campos">
            <div class="campo">
                <label for="Motivo">Motivo de la corrección (obligatorio)</label>
                <textarea id="Motivo" name="Motivo" rows="4" maxlength="500" required>@Model.Formulario.Motivo</textarea>
                <span asp-validation-for="Formulario.Motivo" class="campo__ayuda"></span>
            </div>
        </div>
        <div class="acciones">
            <button type="submit" class="boton boton--primario"
                    data-confirmar="La publicación vigente quedará corregida de forma definitiva y los ajustes se aplicarán. ¿Confirma la corrección?">
                Confirmar corrección
            </button>
            <a class="enlace" asp-action="Detalles" asp-route-id="@Model.Correctiva.Id">Cancelar</a>
        </div>
    </form>
</section>
```

- [ ] **Step 5: Actualizar `Detalles.cshtml` — renombrar Eliminar y enlazar Corregir**

En `Views/PreciosHuevo/Detalles.cshtml`, reemplazar el bloque del botón de
anular:

```html
        @if (Model.PuedeAnularse)
        {
            <form asp-action="Anular" asp-route-id="@publicacion.Id" method="post"
                  data-confirmar="La publicación futura quedará anulada de forma definitiva. ¿Confirma la anulación?">
                <button type="submit" class="boton boton--secundario">Anular publicación</button>
            </form>
        }
```

por:

```html
        @if (Model.PuedeAnularse)
        {
            <form asp-action="Anular" asp-route-id="@publicacion.Id" method="post"
                  data-confirmar="La publicación futura se eliminará de forma definitiva. ¿Confirma la eliminación?">
                <button type="submit" class="boton boton--secundario">Eliminar publicación</button>
            </form>
        }
```

Y, dentro de `@if (Model.PuedeEditarse)` (el bloque que hoy solo tiene
"Editar borrador", "Publicar" y "Eliminar borrador"), agregar el enlace a
Corregir junto a los otros botones de esa sección:

```html
            <a class="boton boton--neutro" asp-action="Corregir" asp-route-id="@publicacion.Id">Corregir la publicación vigente con este borrador</a>
```

Por último, reemplazar el texto del aviso de publicación efectiva:

```html
    @if (Model.EsPublicacionEfectiva)
    {
        <div class="alerta alerta--aviso" role="note">
            Esta publicación ya entró en vigor y es inmutable: no se puede editar,
            borrar ni anular. Si contiene datos incorrectos, importe una publicación
            corregida y publíquela con su propia fecha de vigencia.
        </div>
    }
```

por:

```html
    @if (Model.EsPublicacionEfectiva)
    {
        <div class="alerta alerta--aviso" role="note">
            Esta publicación ya entró en vigor y es inmutable: no se puede editar,
            borrar ni anular directamente. Si contiene datos incorrectos, importe una
            publicación corregida (Excel) y, desde el borrador resultante, use la opción
            "Corregir la publicación vigente con este borrador": aplica los ajustes de
            crédito de los despachos ya recibidos y recongela los que todavía están en
            tránsito.
        </div>
    }
```

- [ ] **Step 6: Correr la suite completa de Trajano.GestorCaisy.Tests**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS (todas).

- [ ] **Step 7: Puerta de calidad completa**

Run: `./verify.ps1` (o `./verify.sh`)
Expected: todos los gates en verde — adaptadores generados, mojibake,
enlaces, frontend (lint/build/tests), backend build, backend tests
(Architecture, Unit, GestorCaisy, Integration).

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Models/PreciosHuevoVistas.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Corregir.cshtml \
  Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs
git commit -m "feat(gestor-caisy): pantalla de correccion de precio de huevo y boton eliminar"
```
