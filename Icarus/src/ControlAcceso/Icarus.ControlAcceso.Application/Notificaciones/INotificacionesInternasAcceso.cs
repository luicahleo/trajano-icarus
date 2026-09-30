using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Notificaciones;

// Puerto de notificaciones internas del módulo ControlAcceso. El alcance va
// explícito: siempre por ClienteId del tenant; no hay bandeja global.
public interface INotificacionesInternasAcceso
{
    void Agregar(NotificacionInternaAcceso notificacion);

    Task<NotificacionInternaAcceso?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<NotificacionInternaAcceso?> ObtenerPorIncidenciaAsync(
        Guid incidenciaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacionInternaAcceso>> ListarAsync(
        Guid clienteId, CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid clienteId, CancellationToken cancellationToken = default);
}
