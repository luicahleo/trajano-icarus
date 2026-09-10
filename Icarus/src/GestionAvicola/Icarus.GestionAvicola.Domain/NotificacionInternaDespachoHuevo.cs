using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Notificación interna del despacho de huevo (spec SP9). DespachoHuevoId es
// nulo para CreditoInsuficiente, que no se origina en un despacho sino en el
// envío de un pedido de alimento con saldo proyectado negativo; en ese caso
// el id del pedido va en Meta. Entidad dedicada y no una extensión de
// NotificacionInterna (SP8): esa clase tiene PedidoId no anulable, indexado
// y expuesto en su DTO — generalizarla implicaría migrar una tabla ya en
// producción para un beneficio menor al costo (ver nota de arquitectura del
// plan).
public sealed class NotificacionInternaDespachoHuevo : Entity
{
    private NotificacionInternaDespachoHuevo()
    {
    }

    private NotificacionInternaDespachoHuevo(
        TipoNotificacionDespachoHuevo tipo, Guid? despachoHuevoId, Guid? clienteId, string? meta)
    {
        Tipo = tipo;
        DespachoHuevoId = despachoHuevoId;
        ClienteId = clienteId;
        Meta = meta;
        FechaUtc = DateTime.UtcNow;
    }

    public static NotificacionInternaDespachoHuevo ParaRecepcionConfirmada(
        Guid despachoHuevoId, Guid clienteId) =>
        new(TipoNotificacionDespachoHuevo.DespachoRecibido, despachoHuevoId, clienteId, null);

    // Bandeja global de CAISY: el pedido de alimento afectado va en Meta
    // porque no hay un despacho de huevo específico que lo origine.
    public static NotificacionInternaDespachoHuevo ParaCreditoInsuficiente(Guid pedidoAlimentoId) =>
        new(TipoNotificacionDespachoHuevo.CreditoInsuficiente, null, null, pedidoAlimentoId.ToString());

    // Ajuste de crédito por corrección de una publicación vigente (spec
    // SP9D): a diferencia de CreditoInsuficiente, sí tiene DespachoHuevoId
    // (el despacho que originó el ajuste) y ClienteId relleno — es del
    // tenant afectado, no de la bandeja global de CAISY. Meta lleva el monto
    // y el motivo en texto, misma convención que CreditoInsuficiente.
    public static NotificacionInternaDespachoHuevo ParaAjusteCredito(
        Guid despachoHuevoId, Guid clienteId, string meta) =>
        new(TipoNotificacionDespachoHuevo.AjusteCredito, despachoHuevoId, clienteId, meta);

    public TipoNotificacionDespachoHuevo Tipo { get; private set; }

    public Guid? DespachoHuevoId { get; private set; }

    public Guid? ClienteId { get; private set; }

    public string? Meta { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public bool Leida { get; private set; }

    public Guid? LeidaPor { get; private set; }

    public DateTime? FechaLeidaUtc { get; private set; }

    public void MarcarLeida(Guid actorId)
    {
        if (Leida)
            return;
        Leida = true;
        LeidaPor = actorId;
        FechaLeidaUtc = DateTime.UtcNow;
    }
}
