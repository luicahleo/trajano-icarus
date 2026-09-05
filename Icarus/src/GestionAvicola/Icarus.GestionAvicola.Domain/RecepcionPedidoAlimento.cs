using System.Text.Json;
using System.Text.Json.Serialization;
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Recepción por línea de un Pedido de alimento (spec SP8C "Despacho, nota y
// recepción"): el tenant registra la cantidad realmente recibida de cada línea
// despachada. La coincidencia completa contra lo entregado termina el pedido
// como RecibidoConforme; cualquier diferencia lo termina como
// RecibidoConDiferencias con el detalle persistido. El total recibido es el
// cálculo canónico del gasto: equivalentes realmente recibidos por el precio
// congelado al envío.
public sealed class RecepcionPedidoAlimento : Entity
{
    private readonly List<DetalleRecepcionPedidoAlimento> _lineas = [];

    private RecepcionPedidoAlimento()
    {
    }

    // Nace con la clave vacía: EF la descubre por la navegación del agregado y
    // la registra como Added (patrón AgregarDetalle del catálogo de precios).
    internal RecepcionPedidoAlimento(
        DateOnly fechaRecepcion, decimal totalRecibido,
        IReadOnlyList<DetalleRecepcionPedidoAlimento> lineas,
        IReadOnlyList<DiferenciaRecepcion> diferencias)
    {
        Id = Guid.Empty;
        FechaRecepcion = fechaRecepcion;
        TotalRecibido = totalRecibido;
        _lineas.AddRange(lineas);
        DiferenciasJson = JsonSerializer.Serialize(diferencias, OpcionesJson);
    }

    public DateOnly FechaRecepcion { get; private set; }

    // Snapshot persistido del gasto canónico (spec SP8C): equivalentes
    // realmente recibidos por línea × precio congelado al envío, calculado por
    // el agregado al confirmar. El precio vigente posterior no lo altera.
    public decimal TotalRecibido { get; private set; }

    public IReadOnlyList<DetalleRecepcionPedidoAlimento> Lineas => _lineas.AsReadOnly();

    // Snapshot de diferencias contra lo despachado (spec SP8C): queda
    // persistido en JSON para el histórico, sin inferir resolución comercial.
    // La lista se reconstruye desde el JSON persistido al materializar.
    public string DiferenciasJson { get; private set; } = string.Empty;

    private List<DiferenciaRecepcion>? _diferenciasMaterializadas;

    public IReadOnlyList<DiferenciaRecepcion> Diferencias =>
        _diferenciasMaterializadas ??= JsonSerializer
            .Deserialize<List<DiferenciaRecepcion>>(DiferenciasJson, OpcionesJson) ?? [];

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}

// Una línea recibida: cantidad entera en la unidad natural de la presentación,
// sin negativos; la referencia a la línea despachada es lógica por tipo.
public sealed class DetalleRecepcionPedidoAlimento : Entity
{
    private DetalleRecepcionPedidoAlimento()
    {
    }

    internal DetalleRecepcionPedidoAlimento(
        TipoAlimento tipoAlimento, PresentacionAlimento presentacion, int cantidadRecibida)
    {
        if (cantidadRecibida < 0)
            throw new ReglaNegocioException("La cantidad recibida no puede ser negativa.");

        Id = Guid.Empty;
        TipoAlimento = tipoAlimento;
        Presentacion = presentacion;
        CantidadRecibida = cantidadRecibida;
        Equivalentes40Kg = presentacion switch
        {
            PresentacionAlimento.Bolsa => cantidadRecibida,
            PresentacionAlimento.Granel => cantidadRecibida * 25,
            _ => throw new ReglaNegocioException("La presentación no es válida."),
        };
    }

    public TipoAlimento TipoAlimento { get; private set; }

    public PresentacionAlimento Presentacion { get; private set; }

    public int CantidadRecibida { get; private set; }

    public int Equivalentes40Kg { get; private set; }
}

// Fila del snapshot de diferencias (persistida junto a la recepción): lo
// recibido contra lo entregado por el distribuidor, en la unidad natural.
public sealed record DiferenciaRecepcion(
    TipoAlimento TipoAlimento,
    int CantidadRecibida,
    int CantidadEntregada,
    int Diferencia);
