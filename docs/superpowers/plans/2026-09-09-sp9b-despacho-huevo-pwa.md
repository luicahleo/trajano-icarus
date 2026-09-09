# SP9B — Despacho de huevo desde la PWA (Borrador → Despachado) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que un cliente o un trabajador autorizado cree, edite,
borre y envíe desde la PWA un despacho de huevos hacia CAISY: máquina de
estados `Borrador → Despachado`, congelamiento del precio vigente por
tamaño y evidencia fotográfica obligatoria de la nota de entrega. La
confirmación de recepción por CAISY, el recibo y el crédito quedan en SP9C.

**Architecture:** Mismo patrón de `PedidoAlimento` (SP8B): agregado de
dominio con métodos explícitos y guardas de estado, congelamiento de precio
resuelto por Application dentro de una transacción, endpoint tenant
protegido por una nueva funcionalidad de `Funcionalidades`. La evidencia
fotográfica reutiliza tal cual el almacén privado ya construido para
pedidos de alimento (`IAlmacenDocumentosPedido`, SP8C): es genérico pese al
nombre (claves Guid opacas, sin colisión entre features) y evita duplicar
almacenamiento/compresión de imágenes.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core (SQL Server),
MediatR, FluentValidation, React + TanStack Query (PWA), xUnit + NSubstitute,
Testcontainers.MsSql.

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake.
- Anti-PII: Seq recibe solo ids técnicos, estados y conteos — nunca el
  contenido ni el nombre completo de la foto.
- TDD estricto; un commit por tarea; `./verify.ps1` antes de cada commit;
  prohibido `--no-verify`. Docker activo para integración.
- Esta feature es **online**, igual que pedidos de alimento: sin IndexedDB,
  service worker ni cola offline (se despacha con conectividad, en el
  momento de cargar el camión).
- `FechaDespacho` la fija el servidor con la fecha de negocio de Bolivia
  (`FechasNegocio.Hoy()`), nunca el cliente.
- El despacho no descuenta ni valida contra `RegistroProduccion` (spec SP9,
  decisión explícita: la producción diaria solo sirve para calcular
  eficiencia).
- `EstadoDespachoHuevo` se define completo desde esta tarea
  (`Borrador = 0, Despachado = 1, Recibido = 2`) para no renumerar más
  adelante, aunque el método que lleva a `Recibido` (`ConfirmarRecepcion`)
  recién se agrega en SP9C — mismo patrón que `EstadoPedidoAlimento` en SP8B.

## Dependencia

SP9A integrado en `develop`, con al menos una `PublicacionPrecioHuevo`
publicada para poder despachar en pruebas de integración.

---

### Task 1: Dominio — `DespachoHuevo` (Borrador → Despachado)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetalleDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TransicionDespachoHuevo.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs`

**Interfaces:**
- Consumes: `TamanoHuevo` (SP9A, `Icarus.GestionAvicola.Domain`).
- Produces: `DespachoHuevo` (ctor `(Guid clienteId, Guid granjaId, Guid
  creadoPor, IReadOnlyList<DatosDetalleDespachoHuevo> detalles)`, ctor con
  `Guid id` para tests), propiedades `ClienteId`, `GranjaId`, `CreadoPor`,
  `Estado` (`EstadoDespachoHuevo`), `EstaActivo`, `Version` (rowversion),
  `FechaDespacho` (`DateOnly?`), `DocumentoNota` (`DocumentoDespachoHuevo?`),
  `TotalAmarras`/`TotalHuevos`/`TotalBs` (`int`/`int`/`decimal?`, calculados),
  `Detalles`, `Historial`; métodos `ReemplazarDetalles(...)`, `Desactivar()`,
  `Despachar(DateOnly fechaDespacho, Guid actorId, IReadOnlyList<DatosPrecioDespachoHuevo>
  precios, DatosDocumentoNota documento)`.
- Produces: `DetalleDespachoHuevo` (`Tamano`, `CantidadAmarras`,
  `UnidadesSueltas`, `CantidadHuevos` calculado, `PrecioProductorCongelado`
  nullable, `PublicacionPrecioHuevoId` nullable, `Subtotal` calculado).
- Produces: `DocumentoDespachoHuevo` (mismos campos que
  `DocumentoNotaEntrega` de SP8D: `ClaveOriginal`, `ClaveVista`, `Mime`,
  `TamanoBytes`, `TamanoVistaBytes`, `HashSha256`, `NombreSeguro`,
  `FechaUtc`) — entidad propia y no reutilización de `DocumentoNotaEntrega`
  porque esa clase está atada por FK a `EntregaPedidoAlimento` (otro
  agregado); el almacén físico sí se reutiliza (ver Task 3).
- Produces: `DatosDetalleDespachoHuevo(TamanoHuevo Tamano, int
  CantidadAmarras, int UnidadesSueltas)`,
  `DatosPrecioDespachoHuevo(TamanoHuevo Tamano, decimal PrecioAlProductor,
  Guid PublicacionPrecioHuevoId)` — records de entrada para que Task 3
  (Application) los construya.
- Produces: `EstadoDespachoHuevo { Borrador = 0, Despachado = 1, Recibido = 2 }`.
- Produces: `TransicionDespachoHuevo` (origen, destino, `ActorId`,
  `FechaUtc`) — sin motivo obligatorio, a diferencia de
  `TransicionPedidoAlimento` (este flujo no tiene devolución ni rechazo).

```csharp
// EstadoDespachoHuevo.cs
namespace Icarus.GestionAvicola.Domain;

// Estados de un despacho de huevo (spec SP9). SP9B cubre Borrador y
// Despachado; Recibido llega en SP9C con el método ConfirmarRecepcion. El
// valor ya se define aquí, sin renumerar, mismo patrón que
// EstadoPedidoAlimento en SP8B.
public enum EstadoDespachoHuevo
{
    Borrador = 0,
    Despachado = 1,
    Recibido = 2,
}
```

```csharp
// TransicionDespachoHuevo.cs
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Fila de historial inmutable de un despacho de huevo (spec SP9). Sin motivo
// obligatorio: a diferencia del pedido de alimento, este flujo no tiene
// devolución ni rechazo.
public sealed class TransicionDespachoHuevo : Entity
{
    private TransicionDespachoHuevo()
    {
    }

    internal TransicionDespachoHuevo(
        EstadoDespachoHuevo origen, EstadoDespachoHuevo destino, Guid actorId)
    {
        Origen = origen;
        Destino = destino;
        ActorId = actorId;
        FechaUtc = DateTime.UtcNow;
    }

    public EstadoDespachoHuevo Origen { get; private set; }

    public EstadoDespachoHuevo Destino { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTime FechaUtc { get; private set; }
}
```

```csharp
// DetalleDespachoHuevo.cs
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Línea del despacho por tamaño (spec SP9). PrecioProductorCongelado y
// PublicacionPrecioHuevoId quedan nulos en Borrador; Despachar los fija.
public sealed class DetalleDespachoHuevo : Entity
{
    private DetalleDespachoHuevo()
    {
    }

    public DetalleDespachoHuevo(TamanoHuevo tamano, int cantidadAmarras, int unidadesSueltas)
    {
        if (cantidadAmarras < 0)
            throw new ReglaNegocioException("La cantidad de amarras no puede ser negativa.");
        if (unidadesSueltas is < 0 or > 179)
            throw new ReglaNegocioException("Las unidades sueltas deben estar entre 0 y 179.");
        if (cantidadAmarras == 0 && unidadesSueltas == 0)
            throw new ReglaNegocioException("Cada línea debe declarar una cantidad mayor que cero.");

        Tamano = tamano;
        CantidadAmarras = cantidadAmarras;
        UnidadesSueltas = unidadesSueltas;
    }

    public TamanoHuevo Tamano { get; private set; }

    public int CantidadAmarras { get; private set; }

    public int UnidadesSueltas { get; private set; }

    public int CantidadHuevos => CantidadAmarras * 180 + UnidadesSueltas;

    public decimal? PrecioProductorCongelado { get; private set; }

    public Guid? PublicacionPrecioHuevoId { get; private set; }

    public decimal? Subtotal => PrecioProductorCongelado is { } precio
        ? CantidadHuevos * precio
        : null;

    internal void CongelarPrecio(decimal precioAlProductor, Guid publicacionPrecioHuevoId)
    {
        PrecioProductorCongelado = precioAlProductor;
        PublicacionPrecioHuevoId = publicacionPrecioHuevoId;
    }
}
```

```csharp
// DespachoHuevo.cs
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Agregado raíz del despacho de huevo (spec SP9). Es un registro del tenant
// (Cliente o Trabajador con la funcionalidad DespachoHuevo); CreadoPor es
// solo auditoría técnica. SP9B cubre Borrador y Despachado; SP9C agrega
// ConfirmarRecepcion.
public sealed class DespachoHuevo : AggregateRoot
{
    private readonly List<DetalleDespachoHuevo> _detalles = [];
    private readonly List<TransicionDespachoHuevo> _historial = [];
    private DocumentoDespachoHuevo? _documentoNota;

    private DespachoHuevo()
    {
    }

    public DespachoHuevo(
        Guid clienteId, Guid granjaId, Guid creadoPor,
        IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        ClienteId = clienteId;
        GranjaId = granjaId;
        CreadoPor = creadoPor;
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public DespachoHuevo(Guid id, Guid clienteId, Guid granjaId, Guid creadoPor,
        IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
        : this(clienteId, granjaId, creadoPor, detalles) => Id = id;

    public Guid ClienteId { get; private set; }

    public Guid GranjaId { get; private set; }

    public Guid CreadoPor { get; private set; }

    public EstadoDespachoHuevo Estado { get; private set; } = EstadoDespachoHuevo.Borrador;

    public bool EstaActivo { get; private set; } = true;

#pragma warning disable S1144 // Setter técnico para EF rowversion
    public byte[]? Version { get; private set; }
#pragma warning restore S1144

    public DateOnly? FechaDespacho { get; private set; }

    public DocumentoDespachoHuevo? DocumentoNota => _documentoNota;

    public IReadOnlyCollection<DetalleDespachoHuevo> Detalles => _detalles.AsReadOnly();

    public IReadOnlyList<TransicionDespachoHuevo> Historial => _historial.AsReadOnly();

    public int TotalAmarras => _detalles.Sum(d => d.CantidadAmarras);

    public int TotalHuevos => _detalles.Sum(d => d.CantidadHuevos);

    // Null mientras el despacho no se haya enviado (el borrador puede
    // construirse sin precios).
    public decimal? TotalBs =>
        _detalles.Count > 0 && _detalles.All(d => d.Subtotal is not null)
            ? _detalles.Sum(d => d.Subtotal!.Value)
            : null;

    // Solo el borrador se edita: reemplaza todas las líneas.
    public void EditarDetalles(IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un borrador se puede editar.");
        ReemplazarDetalles(detalles);
    }

    // Borrado lógico (glosario): solo el borrador se desactiva.
    public void Desactivar()
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un borrador se puede desactivar.");
        EstaActivo = false;
    }

    // Envío a CAISY (spec SP9): el servidor fija la fecha de negocio, congela
    // el precio al productor vigente de cada línea con cantidad y exige la
    // foto de la nota de entrega en la misma operación. Si falta precio para
    // una línea, el envío falla completo y el borrador queda intacto.
    public void Despachar(
        DateOnly fechaDespacho, Guid actorId,
        IReadOnlyList<DatosPrecioDespachoHuevo> precios, DatosDocumentoNota documento)
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un despacho en borrador se puede enviar.");
        var congelados = CongelarPrecios(precios);
        _documentoNota = new DocumentoDespachoHuevo(
            documento.ClaveOriginal, documento.ClaveVista, documento.Mime,
            documento.TamanoBytes, documento.TamanoVistaBytes,
            documento.HashSha256, documento.NombreSeguro);
        foreach (var linea in _detalles)
            linea.CongelarPrecio(congelados[linea.Tamano].PrecioAlProductor, congelados[linea.Tamano].PublicacionPrecioHuevoId);
        Estado = EstadoDespachoHuevo.Despachado;
        FechaDespacho = fechaDespacho;
        RegistrarTransicion(EstadoDespachoHuevo.Borrador, EstadoDespachoHuevo.Despachado, actorId);
    }

    private void AsegurarEstado(EstadoDespachoHuevo esperado, string mensaje)
    {
        if (Estado != esperado)
            throw new ReglaNegocioException(mensaje);
    }

    private void ReemplazarDetalles(IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        if (detalles.Count == 0)
            throw new ReglaNegocioException("El despacho debe tener al menos una línea.");
        if (detalles.GroupBy(d => d.Tamano).Any(g => g.Count() > 1))
            throw new ReglaNegocioException("Cada tamaño solo puede aparecer una vez en el despacho.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetalleDespachoHuevo(datos.Tamano, datos.CantidadAmarras, datos.UnidadesSueltas));
    }

    // Resuelve y valida el precio de cada línea antes de tocar el estado
    // (spec SP9, mismo patrón que PedidoAlimento.CongelarPrecios): un envío
    // sin precio vigente completo no deja rastro.
    private Dictionary<TamanoHuevo, DatosPrecioDespachoHuevo> CongelarPrecios(
        IReadOnlyList<DatosPrecioDespachoHuevo> precios)
    {
        var indice = precios.ToDictionary(p => p.Tamano);
        foreach (var linea in _detalles)
            if (!indice.ContainsKey(linea.Tamano))
                throw new ReglaNegocioException("Falta precio vigente para una línea del despacho.");
        return indice;
    }

    private void RegistrarTransicion(EstadoDespachoHuevo origen, EstadoDespachoHuevo destino, Guid actorId) =>
        _historial.Add(new TransicionDespachoHuevo(origen, destino, actorId));
}

public sealed record DatosDetalleDespachoHuevo(TamanoHuevo Tamano, int CantidadAmarras, int UnidadesSueltas);

public sealed record DatosPrecioDespachoHuevo(
    TamanoHuevo Tamano, decimal PrecioAlProductor, Guid PublicacionPrecioHuevoId);
```

```csharp
// DocumentoDespachoHuevo.cs — agregar al final de DespachoHuevo.cs o en su
// propio archivo; aquí como bloque separado por claridad.
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Respaldo fotográfico de la nota de entrega que el trabajador sube al
// despachar (spec SP9): a diferencia de SP8D (donde el receptor sube la
// foto), aquí la sube el emisor en el mismo paso que envía. Un solo
// documento por despacho, inmutable tras crearse.
public sealed class DocumentoDespachoHuevo : Entity
{
    private DocumentoDespachoHuevo()
    {
    }

    internal DocumentoDespachoHuevo(
        Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes,
        long tamanoVistaBytes, string hashSha256, string nombreSeguro)
    {
        Id = Guid.Empty; // Ver DocumentoNotaEntrega (SP8D): EF lo registra Added por navegación.
        ClaveOriginal = claveOriginal;
        ClaveVista = claveVista;
        Mime = mime;
        TamanoBytes = tamanoBytes;
        TamanoVistaBytes = tamanoVistaBytes;
        HashSha256 = hashSha256;
        NombreSeguro = nombreSeguro;
        FechaUtc = DateTime.UtcNow;
    }

    public Guid ClaveOriginal { get; private set; }

    public Guid ClaveVista { get; private set; }

    public string Mime { get; private set; } = string.Empty;

    public long TamanoBytes { get; private set; }

    public long TamanoVistaBytes { get; private set; }

    public string HashSha256 { get; private set; } = string.Empty;

    public string NombreSeguro { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
```

Nota: `DatosDocumentoNota` ya existe (SP8C,
`Icarus.GestionAvicola.Domain`, en `DocumentoNotaEntrega.cs`) y es un record
genérico (`ClaveOriginal, ClaveVista, Mime, TamanoBytes, TamanoVistaBytes,
HashSha256, NombreSeguro`) — se reutiliza tal cual como parámetro de
`Despachar`, sin crear un record paralelo.

- [ ] **Step 1: Escribir el test de dominio en rojo**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class DespachoHuevoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid GranjaId = Guid.NewGuid();
    private static readonly Guid CreadoPor = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static DatosDocumentoNota Documento() => new(
        Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash", "nota.jpg");

    private static DespachoHuevo BorradorConDosDetalles() =>
        new(ClienteId, GranjaId, CreadoPor,
        [
            new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 2, 0),
            new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 1, 90),
        ]);

    private static List<DatosPrecioDespachoHuevo> PreciosPara(DespachoHuevo despacho, Guid publicacionId) =>
        despacho.Detalles.Select(d => new DatosPrecioDespachoHuevo(d.Tamano, 0.70m, publicacionId)).ToList();

    [Fact]
    public void ElBorradorSeCreaConSusLineas()
    {
        var despacho = BorradorConDosDetalles();

        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        Assert.Equal(2, despacho.Detalles.Count);
        Assert.Null(despacho.TotalBs);
    }

    [Fact]
    public void NoAdmiteDosLineasConElMismoTamano()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [
                new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0),
                new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 2, 0),
            ]));

        Assert.Equal("Cada tamaño solo puede aparecer una vez en el despacho.", excepcion.Message);
    }

    [Fact]
    public void UnaLineaSinCantidadEsRechazada()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 0, 0)]));

        Assert.Equal("Cada línea debe declarar una cantidad mayor que cero.", excepcion.Message);
    }

    [Fact]
    public void UnidadesSueltasFueraDeRangoSonRechazadas()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 180)]));

        Assert.Equal("Las unidades sueltas deben estar entre 0 y 179.", excepcion.Message);
    }

    [Fact]
    public void CantidadHuevosSumaAmarrasYSueltas()
    {
        var despacho = BorradorConDosDetalles();

        var extra = despacho.Detalles.Single(d => d.Tamano == TamanoHuevo.Extra);
        var primera = despacho.Detalles.Single(d => d.Tamano == TamanoHuevo.Primera);
        Assert.Equal(360, extra.CantidadHuevos);
        Assert.Equal(270, primera.CantidadHuevos);
        Assert.Equal(3, despacho.TotalAmarras);
        Assert.Equal(630, despacho.TotalHuevos);
    }

    [Fact]
    public void SoloElBorradorSeEditaOSeDesactiva()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());

        Assert.Throws<ReglaNegocioException>(() =>
            despacho.EditarDetalles([new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]));
        Assert.Throws<ReglaNegocioException>(despacho.Desactivar);
    }

    [Fact]
    public void DespacharCongelaPreciosYCalculaTotales()
    {
        var despacho = BorradorConDosDetalles();
        var publicacionId = Guid.NewGuid();

        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, publicacionId), Documento());

        Assert.Equal(EstadoDespachoHuevo.Despachado, despacho.Estado);
        Assert.Equal(new DateOnly(2026, 11, 5), despacho.FechaDespacho);
        Assert.Equal(630 * 0.70m, despacho.TotalBs);
        Assert.All(despacho.Detalles, d => Assert.Equal(publicacionId, d.PublicacionPrecioHuevoId));
        Assert.NotNull(despacho.DocumentoNota);
        Assert.Single(despacho.Historial);
    }

    [Fact]
    public void DespacharSinPrecioParaUnaLineaFallaCompleto()
    {
        var despacho = BorradorConDosDetalles();
        var precioIncompleto = new List<DatosPrecioDespachoHuevo>
        {
            new(TamanoHuevo.Extra, 0.70m, Guid.NewGuid()),
        };

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, precioIncompleto, Documento()));

        Assert.Equal("Falta precio vigente para una línea del despacho.", excepcion.Message);
        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        Assert.Null(despacho.Detalles.First().PrecioProductorCongelado);
    }

    [Fact]
    public void DespacharDosVecesFalla()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.Despachar(new DateOnly(2026, 11, 6), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento()));

        Assert.Equal("Solo un despacho en borrador se puede enviar.", excepcion.Message);
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevo`
Expected: FAIL con `CS0246` (los tipos del dominio no existen todavía).

- [ ] **Step 2: Implementar los cuatro archivos del dominio**

Con el contenido dado arriba.

- [ ] **Step 3: Correr las pruebas y confirmar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachoHuevo`
Expected: PASS (10/10).

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetalleDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TransicionDespachoHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachoHuevoTests.cs
git commit -m "feat(avicola): modelar despachos de huevo"
```

---

### Task 2: Persistencia y puertos

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetalleDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDocumentoDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionTransicionDespachoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `DespachoHuevo`, `DetalleDespachoHuevo`, `DocumentoDespachoHuevo`,
  `TransicionDespachoHuevo` (Task 1).
- Produces: `IRepositorioDespachosHuevo` — para que Task 3 lo inyecte.

```csharp
// PuertosDespachosHuevo.cs
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Puerto del despacho de huevo (spec SP9). Sin cupo ni conteo bloqueable: a
// diferencia de pedidos de alimento, no hay límite semanal de despachos.
public interface IRepositorioDespachosHuevo
{
    void Agregar(DespachoHuevo despacho);

    void AgregarDetalle(DetalleDespachoHuevo detalle);

    Task<DespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<DespachoHuevo?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DespachoHuevo>> ListarDelTenantAsync(
        CancellationToken cancellationToken = default);
}
```

**EF config** (tres tablas: `despachos_huevo`, `detalles_despacho_huevo`,
`transiciones_despacho_huevo`; el documento va embebido como owned/entity
1:1 igual que las otras — usar entidad separada `documentos_despacho_huevo`
para no mezclar responsabilidades):

```csharp
// ConfiguracionDespachoHuevo.cs
public sealed class ConfiguracionDespachoHuevo : IEntityTypeConfiguration<DespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DespachoHuevo> builder)
    {
        builder.ToTable("despachos_huevo");
        builder.Property(d => d.FechaDespacho).HasColumnType("date");
        builder.Property(d => d.Estado).HasConversion<int>();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasIndex(d => new { d.ClienteId, d.FechaDespacho });
        builder.HasIndex(d => new { d.Estado, d.ClienteId, d.FechaDespacho });

        builder.HasMany(d => d.Detalles).WithOne()
            .HasForeignKey("DespachoHuevoId").IsRequired();
        builder.Navigation(d => d.Detalles).HasField("_detalles");
        builder.HasMany(d => d.Historial).WithOne()
            .HasForeignKey("DespachoHuevoId").IsRequired();
        builder.Navigation(d => d.Historial).HasField("_historial");
        builder.HasOne(d => d.DocumentoNota).WithOne()
            .HasForeignKey<DocumentoDespachoHuevo>("DespachoHuevoId");
        builder.Navigation(d => d.DocumentoNota).HasField("_documentoNota");
    }
}
```

```csharp
// ConfiguracionDetalleDespachoHuevo.cs
public sealed class ConfiguracionDetalleDespachoHuevo : IEntityTypeConfiguration<DetalleDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DetalleDespachoHuevo> builder)
    {
        builder.ToTable("detalles_despacho_huevo");
        builder.Property(d => d.Tamano).HasConversion<int>();
        builder.Property(d => d.PrecioProductorCongelado).HasColumnType("decimal(10,4)");

        builder.HasIndex("DespachoHuevoId", nameof(DetalleDespachoHuevo.Tamano)).IsUnique();
    }
}
```

```csharp
// ConfiguracionDocumentoDespachoHuevo.cs
public sealed class ConfiguracionDocumentoDespachoHuevo : IEntityTypeConfiguration<DocumentoDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DocumentoDespachoHuevo> builder)
    {
        builder.ToTable("documentos_despacho_huevo");
        builder.Property(d => d.Mime).HasMaxLength(100);
        builder.Property(d => d.HashSha256).HasMaxLength(64);
        builder.Property(d => d.NombreSeguro).HasMaxLength(200);

        builder.HasIndex("DespachoHuevoId").IsUnique();
    }
}
```

```csharp
// ConfiguracionTransicionDespachoHuevo.cs
public sealed class ConfiguracionTransicionDespachoHuevo : IEntityTypeConfiguration<TransicionDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<TransicionDespachoHuevo> builder)
    {
        builder.ToTable("transiciones_despacho_huevo");
        builder.Property(t => t.Origen).HasConversion<int>();
        builder.Property(t => t.Destino).HasConversion<int>();
    }
}
```

**Repositorio** — mirror de `RepositorioPedidosAlimento.cs`, sin
`ContarEnviadosEnSemanaBloqueandoAsync`/`IniciarTransaccionAsync` (no hace
falta transacción explícita: `Despachar` no compite por un cupo compartido,
así que `SaveChangesAsync` alcanza):

```csharp
public sealed class RepositorioDespachosHuevo(GestionAvicolaDbContext db) : IRepositorioDespachosHuevo
{
    public void Agregar(DespachoHuevo despacho) => db.DespachosHuevo.Add(despacho);

    public void AgregarDetalle(DetalleDespachoHuevo detalle) =>
        db.Set<DetalleDespachoHuevo>().Add(detalle);

    public async Task<DespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).Include(d => d.DocumentoNota)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<DespachoHuevo?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).Include(d => d.Historial)
            .Include(d => d.DocumentoNota)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DespachoHuevo>> ListarDelTenantAsync(
        CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).ToListAsync(cancellationToken);
}
```

**`GestionAvicolaDbContext.cs`**: agregar `DbSet<DespachoHuevo>
DespachosHuevo => Set<DespachoHuevo>();` y **no** agregar `HasQueryFilter`
por `EstaActivo` con filtro de tenant automático — el aislamiento tenant de
`PedidoAlimento` tampoco usa un filtro global por `ClienteId` (el
repositorio de pedidos no lo tiene, ver `RepositorioPedidosAlimento.cs`):
el filtro de tenant para el despacho lo aplica `ICurrentUser.ClienteId` en
cada handler de Application (Task 3), igual que en pedidos de alimento.

**`DependencyInjection.cs`**: agregar
`servicios.AddScoped<IRepositorioDespachosHuevo, RepositorioDespachosHuevo>();`.

- [ ] **Step 1: Generar la migración y correr integración**

```bash
dotnet ef migrations add DespachosHuevo --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host
```

Verificar que crea las cuatro tablas con los índices e `IsUnique` esperados.

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS (Docker activo; confirma que la migración aplica limpia y no
rompe pruebas existentes).

- [ ] **Step 2: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetalleDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDocumentoDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionTransicionDespachoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/
git commit -m "feat(avicola): persistir despachos de huevo"
```

---

### Task 3: Application — comandos y consultas del tenant

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachosHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioDespachosHuevo` (Task 2),
  `IRepositorioPublicacionesPreciosHuevo` (SP9A),
  `IRepositorioGranjas.ObtenerActivaDelTenantAsync()` (ya existente,
  `Icarus.GestionAvicola.Application.Granjas`), `IAlmacenDocumentosPedido`
  (SP8C, reutilizado — ver nota abajo), `ICurrentUser`,
  `IUnidadTrabajoGestionAvicola`, `IRegistroVuelo`.
- Produces: `CrearBorradorDespachoHuevoCommand`,
  `EditarBorradorDespachoHuevoCommand`, `DesactivarBorradorDespachoHuevoCommand`,
  `DespacharDespachoHuevoCommand`, `ListarDespachosHuevoTenantQuery`,
  `ObtenerDespachoHuevoQuery` — para que Task 4 (API) los invoque.

Reutilización deliberada de `IAlmacenDocumentosPedido` (nombre heredado de
SP8C, pero el contrato es genérico: `GuardarAsync`/`AbrirOriginalAsync`/
`AbrirVistaAsync` con claves Guid opacas). Evita duplicar la validación de
imagen, la compresión a vista y el almacenamiento — mismo criterio ya
aplicado en SP9A con `IAlmacenDocumentosPrecios`.

```csharp
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Fecha de negocio Bolivia (spec SP9); ver la nota de duplicación en
// ComandosPreciosHuevo.cs — cada carpeta de feature se mantiene autocontenida.
public static class FechasNegocio
{
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}

public sealed record LineaDespachoHuevo(string Tamano, int CantidadAmarras, int UnidadesSueltas);

public sealed record CrearBorradorDespachoHuevoCommand(IReadOnlyList<LineaDespachoHuevo> Lineas)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.crear-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadLineas"] = DatoRegistroVuelo.Entero });
}

public sealed record EditarBorradorDespachoHuevoCommand(
    Guid DespachoId, IReadOnlyList<LineaDespachoHuevo> Lineas)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.editar-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadLineas"] = DatoRegistroVuelo.Entero });
}

public sealed record DesactivarBorradorDespachoHuevoCommand(Guid DespachoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.desactivar-borrador", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DespacharDespachoHuevoCommand(
    Guid DespachoId, Stream Contenido, string NombreArchivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.despachar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarDespachosHuevoTenantQuery : IRequest<IReadOnlyList<DespachoHuevoResumen>>;

public sealed record DespachoHuevoResumen(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos, decimal? TotalBs);

public sealed record ObtenerDespachoHuevoQuery(Guid DespachoId) : IRequest<DespachoHuevoDetalle>;

public sealed record DetalleDespachoHuevoResumen(
    Guid Id, string Tamano, int CantidadAmarras, int UnidadesSueltas, int CantidadHuevos,
    decimal? PrecioProductorCongelado, decimal? Subtotal);

public sealed record DespachoHuevoDetalle(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos,
    decimal? TotalBs, IReadOnlyList<DetalleDespachoHuevoResumen> Detalles);

public sealed class CrearBorradorDespachoHuevoValidator : AbstractValidator<CrearBorradorDespachoHuevoCommand>
{
    public CrearBorradorDespachoHuevoValidator() => RuleFor(c => c.Lineas).NotNull().NotEmpty();
}

public sealed class CrearBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRepositorioGranjas granjas,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CrearBorradorDespachoHuevoCommand, Guid>
{
    public async Task<Guid> Handle(CrearBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var granja = await granjas.ObtenerActivaDelTenantAsync(cancellationToken)
            ?? throw new ValidationException("El cliente debe tener una granja activa registrada.");

        var despacho = new DespachoHuevo(clienteId, granja.Id, actorId, ParsearLineas(request.Lineas));
        repositorio.Agregar(despacho);
        registroVuelo.Decidir("avicola.despachos-huevo.crear-borrador", "creacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadLineas"] = despacho.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return despacho.Id;
    }

    internal static IReadOnlyList<DatosDetalleDespachoHuevo> ParsearLineas(
        IReadOnlyList<LineaDespachoHuevo> lineas) =>
        lineas.Select(l =>
        {
            if (!Enum.TryParse<TamanoHuevo>(l.Tamano, true, out var tamano))
                throw new ValidationException("El tamaño de huevo indicado no existe.");
            return new DatosDetalleDespachoHuevo(tamano, l.CantidadAmarras, l.UnidadesSueltas);
        }).ToList();
}

public sealed class EditarBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<EditarBorradorDespachoHuevoCommand>
{
    public async Task Handle(EditarBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        despacho.EditarDetalles(CrearBorradorDespachoHuevoHandler.ParsearLineas(request.Lineas));
        foreach (var detalle in despacho.Detalles)
            repositorio.AgregarDetalle(detalle);
        registroVuelo.Decidir("avicola.despachos-huevo.editar-borrador", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadLineas"] = despacho.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DesactivarBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarBorradorDespachoHuevoCommand>
{
    public async Task Handle(DesactivarBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Borrador)
            throw new ConflictException("Solo un borrador se puede desactivar.");
        despacho.Desactivar();
        registroVuelo.Decidir("avicola.despachos-huevo.desactivar-borrador", "borrado", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Despachar (spec SP9): congela el precio al productor vigente por tamaño y
// exige la foto de la nota en la misma operación. Si falla el guardado del
// archivo, la excepción propaga antes de tocar el agregado (mismo orden que
// ConfirmarRecepcionPedidoHandler en SP8C/D): no queda transición a medias.
public sealed class DespacharDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IAlmacenDocumentosPedido almacen,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DespacharDespachoHuevoCommand>
{
    public async Task Handle(DespacharDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Borrador)
            throw new ConflictException("Solo un despacho en borrador se puede enviar.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        var vigente = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken)
            ?? throw new ValidationException("No hay una publicación de precios de huevo vigente.");
        var precios = vigente.Detalles
            .Select(d => new DatosPrecioDespachoHuevo(d.Tamano, d.PrecioAlProductor, vigente.Id))
            .ToList();

        var guardado = await almacen.GuardarAsync(request.Contenido, cancellationToken);
        var documento = new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, SanearNombre(request.NombreArchivo));

        despacho.Despachar(hoy, actorId, precios, documento);
        registroVuelo.Decidir("avicola.despachos-huevo.despachar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = despacho.Detalles.Count,
                ["PublicacionPrecioHuevoId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    private static string SanearNombre(string? nombreArchivo)
    {
        var nombre = Path.GetFileName(nombreArchivo?.Trim() ?? string.Empty);
        var sano = new string(nombre
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '-')
            .ToArray())
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', ' ');
        if (sano.Length > 200) sano = sano[^200..];
        return sano.Length == 0 ? "nota-despacho.jpg" : sano;
    }
}

public sealed class ListarDespachosHuevoTenantHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoTenantQuery, IReadOnlyList<DespachoHuevoResumen>>
{
    public async Task<IReadOnlyList<DespachoHuevoResumen>> Handle(
        ListarDespachosHuevoTenantQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarDelTenantAsync(cancellationToken))
            .OrderByDescending(d => d.FechaDespacho)
            .ThenByDescending(d => d.Id)
            .Select(d => new DespachoHuevoResumen(
                d.Id, d.Estado.ToString(), d.FechaDespacho, d.TotalAmarras, d.TotalHuevos, d.TotalBs))
            .ToList();
}

public sealed class ObtenerDespachoHuevoHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ObtenerDespachoHuevoQuery, DespachoHuevoDetalle>
{
    public async Task<DespachoHuevoDetalle> Handle(
        ObtenerDespachoHuevoQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        return new DespachoHuevoDetalle(
            despacho.Id, despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs,
            despacho.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetalleDespachoHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.CantidadAmarras, d.UnidadesSueltas,
                    d.CantidadHuevos, d.PrecioProductorCongelado, d.Subtotal))
                .ToList());
    }
}
```

- [ ] **Step 1: Escribir los tests de handlers en rojo**

Cubrir como mínimo (mockeando `IRepositorioDespachosHuevo`,
`IRepositorioGranjas`, `IRepositorioPublicacionesPreciosHuevo`,
`IAlmacenDocumentosPedido`, `ICurrentUser` con NSubstitute — mismo patrón que
`PreciosHuevoHandlerTests.cs` de SP9A):
- `CrearBorradorDespachoHuevoHandler` lanza `ValidationException` cuando el
  tenant no tiene granja activa.
- `DespacharDespachoHuevoHandler` lanza `ConflictException` si el estado no
  es `Borrador`.
- `DespacharDespachoHuevoHandler` lanza `ValidationException` sin publicación
  vigente, y no llama a `almacen.GuardarAsync` en ese caso (verificar con
  `almacen.DidNotReceive()`).
- `DespacharDespachoHuevoHandler` congela el precio y pasa a `Despachado`
  cuando todo está completo.
- `ObtenerDespachoHuevoHandler` lanza `NotFoundException` con un id
  inexistente.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachosHuevoHandler`
Expected: FAIL (tipos de `ComandosDespachosHuevo.cs` inexistentes).

- [ ] **Step 2: Implementar `ComandosDespachosHuevo.cs`**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter DespachosHuevoHandler`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/DespachosHuevoHandlerTests.cs
git commit -m "feat(avicola): comandos de despacho de huevo del tenant"
```

---

### Task 4: API tenant — `/despachos-huevo` y funcionalidad `DespachoHuevo`

**Files:**
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/Funcionalidades.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`
- Create: `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/DespachosHuevoEndpointsTests.cs`

**Interfaces:**
- Consumes: comandos/queries de Task 3, `PoliticasClientes.Para`.
- Produces: rutas HTTP bajo `/despachos-huevo` para que la PWA (Task 5)
  las consuma.

En `Funcionalidades.cs` agregar, sin renumerar:

```csharp
    // SP9: despacho de huevos hacia CAISY. Bit nuevo sin renumerar.
    DespachoHuevo = 512,
```

En `FuncionalidadesModulos.cs`, junto a la línea de `PedidoAlimento`,
agregar `Funcionalidades.DespachoHuevo => Modulos.GestionAvicola,` a la
misma expresión `switch`.

**`DespachosHuevoEndpoints.cs`** — mirror del grupo tenant de
`PedidosAlimentoEndpoints.cs`, con `.MapPost("/{id:guid}/despachar", ...)`
usando el mismo patrón multipart que `.MapPost("/{id:guid}/recibir", ...)`
(`IFormFile` + validación de tamaño/presencia, sin campo `lineas` porque acá
no hay líneas que declarar en el despacho — ya están en el borrador):

```csharp
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Despacho de huevo del tenant (spec SP9): reservado a la funcionalidad
// DespachoHuevo. Solo Borrador y Despachado; Recibido llega en SP9C.
public static class DespachosHuevoEndpoints
{
    private const long TamanoMaximoImagen = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDespachosHuevo(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasClientes.Para(Funcionalidades.DespachoHuevo);
        var tenant = app.MapGroup("/despachos-huevo").RequireAuthorization(politica);

        tenant.MapPost("/", async (GuardarDespachoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(
                new CrearBorradorDespachoHuevoCommand(cuerpo.Lineas), cancellationToken);
            return Results.Created($"/despachos-huevo/{id}", new { id });
        });

        tenant.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarDespachosHuevoTenantQuery(), cancellationToken)));

        // Precio vigente para la PWA (spec SP9): el catálogo en sí solo lo
        // administra CAISY (`/precios-huevo-caisy`, SP9A), pero el tenant
        // necesita conocer el precio antes de despachar. Mismo patrón que
        // `tenant.MapGet("/precios-vigentes", ...)` en
        // PedidosAlimentoEndpoints.cs.
        tenant.MapGet("/precios-vigentes", async Task<IResult> (
            ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioHuevoVigenteQuery(null), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });

        tenant.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoQuery(id), cancellationToken)));

        tenant.MapPut("/{id:guid}", async (Guid id, GuardarDespachoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new EditarBorradorDespachoHuevoCommand(id, cuerpo.Lineas), cancellationToken);
            return Results.NoContent();
        });

        tenant.MapDelete("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DesactivarBorradorDespachoHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Despachar: foto obligatoria de la nota de entrega en la misma
        // operación (spec SP9), sin antiforgery porque la autenticación es
        // Bearer, no cookie.
        tenant.MapPost("/{id:guid}/despachar", async Task<IResult> (
            Guid id, IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta la foto de la nota de entrega." });
            if (archivo.Length > TamanoMaximoImagen)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await using var contenido = archivo.OpenReadStream();
            await mediator.Send(
                new DespacharDespachoHuevoCommand(id, contenido, archivo.FileName), cancellationToken);
            return Results.NoContent();
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoImagen));

        return app;
    }

    private sealed record GuardarDespachoRequest(IReadOnlyList<LineaDespachoHuevo> Lineas);
}
```

En `Program.cs`, junto a `api.MapPreciosHuevo();`:

```csharp
api.MapDespachosHuevo();
```

- [ ] **Step 1: Escribir las pruebas de integración en rojo**

Cubrir: crear/editar/desactivar borrador; `GET /precios-vigentes` devuelve
la publicación activa o 404 sin ninguna vigente; 403 sin la funcionalidad
`DespachoHuevo`; despachar sin publicación vigente da 400; despachar sin
archivo da 400; archivo mayor al límite da 413; despachar con éxito pasa a
`Despachado` y el detalle expone `PrecioProductorCongelado` y `Subtotal`;
segundo despachar da 409; sin granja activa da 400.

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter DespachosHuevoEndpointsTests`
Expected: FAIL — 404 (ruta no registrada).

- [ ] **Step 2: Implementar el endpoint, el bit de funcionalidad y registrar la ruta**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter DespachosHuevoEndpointsTests`
Expected: PASS.

- [ ] **Step 3: Correr toda la suite de integración y unitarios**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests` y
`dotnet test Icarus/tests/Icarus.UnitTests --filter Funcionalidades`
Expected: PASS (confirma que agregar el bit no rompe pruebas de
`Funcionalidades`/asignación a trabajadores existentes).

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Clientes/Icarus.Clientes.Domain/Funcionalidades.cs \
  Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs \
  Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs \
  Icarus/src/Host/Icarus.Host/Program.cs \
  Icarus/tests/Icarus.IntegrationTests/DespachosHuevoEndpointsTests.cs
git commit -m "feat(api): exponer despachos de huevo del tenant"
```

---

### Task 5: PWA — bandeja y formulario de despacho

**Files:**
- Create: `web/src/features/despacho-huevo/api.ts`
- Create: `web/src/features/despacho-huevo/constantes.ts`
- Create: `web/src/features/despacho-huevo/DespachosHuevoPage.tsx`
- Create: `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.tsx`
- Create: `web/src/features/despacho-huevo/DespachoHuevoDetallePage.tsx`
- Modify: `web/src/app/router.tsx`, `web/src/app/navegacion.tsx`,
  `web/src/app/paginasDiferidas.tsx`, `web/src/lib/tipos.ts`
- Create tests colocados junto a cada página/componente.

**Interfaces:**
- Consumes: endpoints de Task 4 (`/despachos-huevo`).

Mirror casi 1:1 de `web/src/features/pedidos-alimento/` (SP8B Tarea 5):
React Query para listar/crear/editar/desactivar borradores, `DialogoConfirmacion`
antes de despachar (acción irreversible), resumen en vivo de amarras/huevos/Bs
mientras el usuario completa cantidades por tamaño, mostrando el precio
vigente obtenido de `GET /despachos-huevo/precios-vigentes` (Task 4) antes de
que el usuario confirme el envío.

- Compresión client-side de la foto de la nota antes de subirla: copiar la
  función `comprimirImagen` de
  `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx:51-71`
  (createImageBitmap + canvas a JPEG 0.7 calidad, máximo 1600px de lado,
  con fallback silencioso al archivo original).
- Sin cola offline, sin IndexedDB — misma feature online que pedidos de
  alimento.
- Tabla de detalle por tamaño con `Cantidad Amarras`, `Unidades Sueltas`,
  `Total Huevos`, `Precio` (solo visible tras despachar), `Subtotal`.

- [ ] **Step 1: Escribir los tests de la feature en rojo**

Mirror de los tests existentes de `pedidos-alimento` (mismo patrón de
mocking de `api.ts` con `vi.mock`, o el helper de fetch falso que ya use el
proyecto). Cubrir como mínimo: la bandeja lista los despachos del tenant con
su estado; el formulario crea un borrador con líneas por tamaño; editar y
borrar solo están disponibles en `Borrador`; despachar exige seleccionar una
foto y muestra el precio vigente antes de confirmar; tras despachar la
página de detalle muestra amarras/huevos/Bs y ya no permite editar.

Run: `npm test -- --run despacho-huevo` (desde `web/`)
Expected: FAIL (los archivos de la feature no existen).

- [ ] **Step 2: Implementar la feature completa**

Run: `npm test -- --run despacho-huevo`
Expected: PASS.

- [ ] **Step 3: Typecheck, lint, build y suite completa**

Run (desde `web/`): `npm run typecheck`, `npm run lint`, `npm run build`,
`npm test -- --run`
Expected: todos en verde.

- [ ] **Step 4: Commit**

```bash
git add web/src/features/despacho-huevo/ web/src/app/router.tsx \
  web/src/app/navegacion.tsx web/src/app/paginasDiferidas.tsx web/src/lib/tipos.ts
git commit -m "feat(web): gestionar despachos de huevo"
```

---

## Cierre SP9B

- [ ] Ejecutar toda la suite: `dotnet test Icarus/tests/Icarus.UnitTests`,
  `dotnet test Icarus/tests/Icarus.IntegrationTests` (Docker activo),
  `dotnet test Icarus/tests/Icarus.ArchitectureTests`,
  `npm test -- --run` (desde `web/`).
- [ ] Ejecutar `./verify.ps1`, revisar `git diff --check` y el diff completo.
- [ ] Actualizar el glosario de dominio si hace falta precisar algún término
  nuevo ("Despacho de huevo", "Documento de nota de entrega").
- [ ] Push a `develop` solo cuando las cinco tareas estén verdes.

**No empezar SP9C (recepción, recibo y crédito) hasta que este plan esté
cerrado e integrado en `develop`.**
