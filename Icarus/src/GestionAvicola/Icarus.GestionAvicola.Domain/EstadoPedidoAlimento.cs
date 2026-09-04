namespace Icarus.GestionAvicola.Domain;

// Estados de un Pedido de alimento (spec SP8). SP8B cubre hasta Aceptado y
// SP8C agregará los estados de despacho y recepción al final de la lista, sin
// renumerar los existentes. Valores estables porque se persisten como entero.
public enum EstadoPedidoAlimento
{
    Borrador = 0,
    Solicitado = 1,
    Rechazado = 2,
    Aceptado = 3,
}
