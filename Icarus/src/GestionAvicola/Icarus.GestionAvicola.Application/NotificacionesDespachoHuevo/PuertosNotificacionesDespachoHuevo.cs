using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public interface INotificacionesInternasDespachoHuevo
{
    void Agregar(NotificacionInternaDespachoHuevo notificacion);

    Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);
}
