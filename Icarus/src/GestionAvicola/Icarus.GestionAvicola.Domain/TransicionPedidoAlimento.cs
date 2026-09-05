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
        // La transición nace con la clave vacía: EF la descubre por la
        // navegación del agregado y, con la clave sin asignar, la registra
        // como Added y genera el Guid al insertar (con clave ya asignada la
        // marcaría Modified y fallaría, mismo caso que AgregarDetalle del
        // catálogo de precios).
        Id = Guid.Empty;
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
