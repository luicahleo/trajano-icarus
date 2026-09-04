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
    // SP8C: estados de despacho y recepción agregados al final, sin renumerar
    // los persistidos. Ambos estados recibidos son terminales.
    Despachado = 4,
    RecibidoConforme = 5,
    RecibidoConDiferencias = 6,
}
