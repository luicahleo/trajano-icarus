using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Una línea de la entrega registrada por CAISY (spec SP8C): cantidad entera en
// la unidad natural de la presentación del pedido (bolsas o toneladas), sin
// negativos. La referencia a la línea solicitada es lógica por tipo: cada tipo
// aparece una sola vez en el pedido. La diferencia contra lo solicitado es
// válida y se calcula, no se corrige.
public sealed class DetalleEntregaPedidoAlimento : Entity
{
    private DetalleEntregaPedidoAlimento()
    {
    }

    // Nace con la clave vacía: EF la descubre por la navegación del agregado y
    // la registra como Added (patrón AgregarDetalle del catálogo de precios).
    internal DetalleEntregaPedidoAlimento(
        TipoAlimento tipoAlimento, PresentacionAlimento presentacion, int cantidadEntregada)
    {
        if (cantidadEntregada < 0)
            throw new ReglaNegocioException("La cantidad entregada no puede ser negativa.");

        Id = Guid.Empty;
        TipoAlimento = tipoAlimento;
        Presentacion = presentacion;
        CantidadEntregada = cantidadEntregada;
        Equivalentes40Kg = presentacion switch
        {
            PresentacionAlimento.Bolsa => cantidadEntregada,
            PresentacionAlimento.Granel => cantidadEntregada * 25,
            _ => throw new ReglaNegocioException("La presentación no es válida."),
        };
    }

    public TipoAlimento TipoAlimento { get; private set; }

    public PresentacionAlimento Presentacion { get; private set; }

    public int CantidadEntregada { get; private set; }

    public int Equivalentes40Kg { get; private set; }
}
