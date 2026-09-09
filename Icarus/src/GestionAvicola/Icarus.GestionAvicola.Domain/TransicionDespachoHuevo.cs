using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Fila de historial inmutable de un despacho de huevo (spec SP9). Sin motivo
// obligatorio: a diferencia del pedido de alimento, este flujo no tiene
// devolución ni rechazo.
public sealed class TransicionDespachoHuevo : Entity
{
    private TransicionDespachoHuevo()
    {
    }

    internal TransicionDespachoHuevo(
        EstadoDespachoHuevo origen, EstadoDespachoHuevo destino, Guid actorId)
    {
        // La transición nace con la clave vacía: EF la descubre por la
        // navegación del agregado y, con la clave sin asignar, la registra
        // como Added y genera el Guid al insertar (con clave ya asignada la
        // marcaría Modified y fallaría, mismo caso que TransicionPedidoAlimento
        // en SP8).
        Id = Guid.Empty;
        Origen = origen;
        Destino = destino;
        ActorId = actorId;
        FechaUtc = DateTime.UtcNow;
    }

    public EstadoDespachoHuevo Origen { get; private set; }

    public EstadoDespachoHuevo Destino { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTime FechaUtc { get; private set; }
}
