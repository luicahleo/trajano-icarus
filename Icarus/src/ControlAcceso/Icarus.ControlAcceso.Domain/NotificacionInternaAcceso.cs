using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Notificación interna de ControlAcceso para el Cliente del tenant. Texto
// genérico: la UI construye el mensaje visible; nunca guarda foto, identidad
// supuesta ni motivo biométrico. Se crea junto con la incidencia.
public sealed class NotificacionInternaAcceso : Entity
{
    private NotificacionInternaAcceso()
    {
    }

    private NotificacionInternaAcceso(Guid incidenciaId, Guid clienteId)
    {
        if (incidenciaId == Guid.Empty)
            throw new ReglaNegocioException("La notificación requiere una incidencia.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La notificación requiere un destinatario.");

        IncidenciaId = incidenciaId;
        ClienteId = clienteId;
        FechaUtc = DateTime.UtcNow;
    }

    public static NotificacionInternaAcceso ParaIncidencia(
        Guid incidenciaId, Guid clienteId) =>
        new(incidenciaId, clienteId);

    public Guid IncidenciaId { get; private set; }

    public Guid ClienteId { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public bool Leida { get; private set; }

    public Guid? LeidaPor { get; private set; }

    public DateTime? FechaLeidaUtc { get; private set; }

    // Idempotente: reintentos HTTP no duplican la marca.
    public void MarcarLeida(Guid actorId)
    {
        if (Leida)
            return;
        Leida = true;
        LeidaPor = actorId;
        FechaLeidaUtc = DateTime.UtcNow;
    }
}
