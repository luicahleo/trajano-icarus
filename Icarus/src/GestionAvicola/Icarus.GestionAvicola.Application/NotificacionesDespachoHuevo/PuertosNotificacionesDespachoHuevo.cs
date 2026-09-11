using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public interface INotificacionesInternasDespachoHuevo
{
    void Agregar(NotificacionInternaDespachoHuevo notificacion);

    Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    // tiposVisibles llega resuelto por rol desde
    // VisibilidadNotificacionesDespachoHuevo y se aplica en SQL (spec SP9F):
    // el listado y el contador filtran por lo mismo, así que no pueden
    // desincronizarse ni traer filas que el usuario no puede ver.
    Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default);
}
