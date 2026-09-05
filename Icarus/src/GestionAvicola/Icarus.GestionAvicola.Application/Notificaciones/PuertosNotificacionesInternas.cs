using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Notificaciones;

// Puerto de las notificaciones internas (spec SP8). El alcance va siempre
// explícito: Guid? clienteId donde null significa la bandeja global de CAISY.
// No hay filtro de tenant en el DbContext para esta entidad: el repositorio
// obliga a pasar el alcance, de modo que ninguna consulta se escape de él.
public interface INotificacionesInternas
{
    void Agregar(NotificacionInterna notificacion);

    Task<NotificacionInterna?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacionInterna>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default);
}
