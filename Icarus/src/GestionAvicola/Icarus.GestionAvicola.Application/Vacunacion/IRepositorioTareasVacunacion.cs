using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Todas las consultas respetan los filtros globales (EstaActivo + tenant):
// un id ajeno o inactivo devuelve null/vacío, igual que uno inexistente
// (anti-enumeración). La desactivación de pendientes preserva completadas y
// canceladas: son el historial sanitario del lote (spec SP7).
public interface IRepositorioTareasVacunacion
{
    void Agregar(TareaVacunacion tarea);

    Task<TareaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Historial del galpón: todas las tareas activas, de cualquier estado,
    // ordenadas por fecha programada.
    Task<IReadOnlyList<TareaVacunacion>> ListarPorGalponAsync(Guid galponId, CancellationToken cancellationToken = default);

    // Notificación: pendientes con FechaProgramada <= hasta (las vencidas no
    // desaparecen). El clienteId se pasa explícito además del filtro global.
    Task<IReadOnlyList<TareaVacunacion>> ListarNotificacionAsync(
        Guid clienteId, DateOnly hoy, DateOnly hasta, CancellationToken cancellationToken = default);

    // Soft delete de las pendientes del galpón; devuelve cuántas se desactivaron.
    Task<int> DesactivarPendientesDeGalponAsync(Guid galponId, CancellationToken cancellationToken = default);

    // Soft delete de las pendientes de un programa (todos los galpones que lo
    // tienen asignado); devuelve cuántas se desactivaron. Completadas y
    // canceladas quedan como historial sanitario (spec SP7).
    Task<int> DesactivarPendientesDeProgramaAsync(Guid programaId, CancellationToken cancellationToken = default);
}

public sealed record TareaVacunacionResumen(
    Guid Id, Guid GalponId, int EdadDia, string Vacuna, string? ModoAplicacion,
    DateOnly FechaProgramada, string Estado, DateOnly? FechaAplicacion, int? AvesVacunadas,
    string? ObservacionesProgramadas, string? ObservacionesAplicacion, string? MotivoCancelacion);

public sealed record NotificacionVacunacionResumen(
    IReadOnlyList<TareaVacunacionResumen> VencidasYHoy,
    IReadOnlyList<TareaVacunacionResumen> Proximas);
