namespace Icarus.ControlAcceso.Application.Notificaciones;

// Resumen de notificación interna de ControlAcceso. El texto visible se
// construye en la UI; el backend solo entrega metadatos genéricos.
public sealed record NotificacionAccesoResumen(
    Guid Id,
    Guid IncidenciaId,
    string Tipo,
    DateTime FechaUtc,
    bool Leida);
