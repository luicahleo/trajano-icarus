using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Una línea de un Pedido de alimento (spec SP8). La cantidad va en la unidad
// natural: bolsas enteras o toneladas enteras. Los equivalentes de 40 kg se
// calculan al crear la línea (una bolsa es un equivalente, una tonelada son
// veinticinco). El precio final por 40 kg, la notificación que lo publicó y el
// subtotal son snapshot: se congelan al enviar y nunca se recalculan desde
// valores impresos redondeados.
public sealed class DetallePedidoAlimento : Entity
{
    private DetallePedidoAlimento()
    {
    }

    public DetallePedidoAlimento(TipoAlimento tipoAlimento, PresentacionAlimento presentacion, int cantidadSolicitada)
    {
        if (cantidadSolicitada <= 0)
            throw new ReglaNegocioException("La cantidad solicitada debe ser mayor que cero.");

        TipoAlimento = tipoAlimento;
        Presentacion = presentacion;
        CantidadSolicitada = cantidadSolicitada;
        Equivalentes40Kg = presentacion switch
        {
            PresentacionAlimento.Bolsa => cantidadSolicitada,
            PresentacionAlimento.Granel => cantidadSolicitada * 25,
            _ => throw new ReglaNegocioException("La presentación no es válida."),
        };
    }

    public TipoAlimento TipoAlimento { get; private set; }

    public PresentacionAlimento Presentacion { get; private set; }

    public int CantidadSolicitada { get; private set; }

    public int Equivalentes40Kg { get; private set; }

    public decimal? PrecioFinalPor40Kg { get; private set; }

    public Guid? NotificacionPreciosAlimentosId { get; private set; }

    public decimal? SubtotalSolicitado { get; private set; }

    // Congelado al enviar (spec SP8): el agregado lo invoca con el precio
    // vigente resuelto por FechaPedido; nunca se recalcula después.
    internal void CongelarPrecio(decimal precioFinalPor40Kg, Guid notificacionPreciosAlimentosId)
    {
        PrecioFinalPor40Kg = precioFinalPor40Kg;
        NotificacionPreciosAlimentosId = notificacionPreciosAlimentosId;
        SubtotalSolicitado = precioFinalPor40Kg * Equivalentes40Kg;
    }
}
