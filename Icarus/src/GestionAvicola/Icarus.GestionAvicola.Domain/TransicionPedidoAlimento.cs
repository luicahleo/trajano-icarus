using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Fila del historial de un Pedido de alimento (spec SP8): conserva estado
// origen y destino, fecha UTC, actor técnico, motivo cuando la transición lo
// exige y valores relevantes como la entrega estimada. Los motivos se muestran
// a usuarios autorizados y nunca salen hacia Seq.
public sealed class TransicionPedidoAlimento : Entity
{
    private TransicionPedidoAlimento()
    {
    }

    public TransicionPedidoAlimento(
        EstadoPedidoAlimento estadoOrigen, EstadoPedidoAlimento estadoDestino,
        Guid actorId, string? motivo, DateOnly? fechaEntregaEstimada)
    {
        EstadoOrigen = estadoOrigen;
        EstadoDestino = estadoDestino;
        ActorId = actorId;
        Motivo = motivo;
        FechaEntregaEstimada = fechaEntregaEstimada;
        FechaUtc = DateTime.UtcNow;
    }

    public EstadoPedidoAlimento EstadoOrigen { get; private set; }

    public EstadoPedidoAlimento EstadoDestino { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public Guid ActorId { get; private set; }

    public string? Motivo { get; private set; }

    public DateOnly? FechaEntregaEstimada { get; private set; }
}
