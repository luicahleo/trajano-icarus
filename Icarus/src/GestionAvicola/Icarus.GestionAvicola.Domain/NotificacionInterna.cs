using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Notificación interna persistente de un pedido (spec SP8). El destinatario
// técnico es el tenant (ClienteId) o, cuando es nula, la bandeja global de
// CAISY. Conserva tipo, pedido y metadatos técnicos en Meta: el texto visible
// lo construye la UI y los motivos no se duplican (viven en el historial del
// pedido) ni llegan a Seq.
public sealed class NotificacionInterna : Entity
{
    private NotificacionInterna()
    {
    }

    private NotificacionInterna(TipoNotificacionPedido tipo, Guid pedidoId, Guid? clienteId, string? meta)
    {
        Tipo = tipo;
        PedidoId = pedidoId;
        ClienteId = clienteId;
        Meta = meta;
        FechaUtc = DateTime.UtcNow;
    }

    // Bandeja global de CAISY (sin tenant).
    public static NotificacionInterna ParaCaisy(
        TipoNotificacionPedido tipo, Guid pedidoId, string? meta = null) =>
        new(tipo, pedidoId, null, meta);

    // Bandeja compartida del tenant.
    public static NotificacionInterna ParaTenant(
        TipoNotificacionPedido tipo, Guid pedidoId, Guid clienteId, string? meta = null) =>
        new(tipo, pedidoId, clienteId, meta);

    public TipoNotificacionPedido Tipo { get; private set; }

    public Guid PedidoId { get; private set; }

    public Guid? ClienteId { get; private set; }

    public string? Meta { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public bool Leida { get; private set; }

    public Guid? LeidaPor { get; private set; }

    public DateTime? FechaLeidaUtc { get; private set; }

    // Idempotente: los reintentos no duplican la marca ni alteran la fecha.
    public void MarcarLeida(Guid actorId)
    {
        if (Leida)
            return;
        Leida = true;
        LeidaPor = actorId;
        FechaLeidaUtc = DateTime.UtcNow;
    }
}
