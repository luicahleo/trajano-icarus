namespace Icarus.GestionAvicola.Domain;

// Tipos de notificación interna de pedidos (spec SP8). La bandeja CAISY
// recibe el envío o reenvío y el tenant las decisiones de CAISY. SP8C agregará
// los tipos de despacho y recepción al final de la lista, sin renumerar. La
// UI compone el mensaje localizado: la notificación solo conserva datos
// técnicos.
public enum TipoNotificacionPedido
{
    PedidoSolicitado = 0,
    PedidoReenviado = 1,
    PedidoDevuelto = 2,
    PedidoRechazado = 3,
    PedidoAceptado = 4,
    EntregaEstimadaActualizada = 5,
    // SP8C: despacho y recepción agregados al final, sin renumerar.
    PedidoDespachado = 6,
    RecepcionConforme = 7,
    RecepcionConDiferencias = 8,
}
