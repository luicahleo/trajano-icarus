using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Entrega única de un Pedido de alimento (spec SP8C "Despacho, nota y
// recepción"): CAISY registra una sola entrega con una sola nota, con datos
// manuales y varias líneas con la cantidad entregada. El total neto informado
// de la nota se conserva para contraste; el cálculo canónico del gasto sigue
// siendo el del dominio con la cantidad real y el precio congelado al envío.
public sealed class EntregaPedidoAlimento : Entity
{
    private readonly List<DetalleEntregaPedidoAlimento> _lineas = [];

    private EntregaPedidoAlimento()
    {
    }

    // Nace con la clave vacía: EF la descubre por la navegación del agregado y
    // la registra como Added (patrón AgregarDetalle del catálogo de precios).
    internal EntregaPedidoAlimento(
        string numeroNota, DateOnly fechaNota, DateOnly fechaDespacho,
        decimal? totalNetoInformado,
        IReadOnlyList<DetalleEntregaPedidoAlimento> lineas)
    {
        Id = Guid.Empty;
        NumeroNota = numeroNota;
        FechaNota = fechaNota;
        FechaDespacho = fechaDespacho;
        TotalNetoInformado = totalNetoInformado;
        _lineas.AddRange(lineas);
    }

    public string NumeroNota { get; private set; } = string.Empty;

    public DateOnly FechaNota { get; private set; }

    public DateOnly FechaDespacho { get; private set; }

    public decimal? TotalNetoInformado { get; private set; }

    public IReadOnlyList<DetalleEntregaPedidoAlimento> Lineas => _lineas.AsReadOnly();
}
