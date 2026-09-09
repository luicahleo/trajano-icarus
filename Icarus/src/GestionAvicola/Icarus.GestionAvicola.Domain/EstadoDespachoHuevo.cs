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
