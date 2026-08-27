using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

public sealed record ListarTareasPorGalponQuery(Guid GalponId)
    : IRequest<IReadOnlyList<TareaVacunacionResumen>>;

public sealed record ListarNotificacionVacunacionQuery() : IRequest<NotificacionVacunacionResumen>;

// Historial sanitario del galpón (spec SP7): todas las tareas activas con su
// estado, ordenadas por fecha programada, decoradas con el nombre del programa.
public sealed class ListarTareasPorGalponHandler(
    IRepositorioGalpones galpones, IRepositorioTareasVacunacion tareas,
    IRepositorioProgramasVacunacion programas)
    : IRequestHandler<ListarTareasPorGalponQuery, IReadOnlyList<TareaVacunacionResumen>>
{
    public async Task<IReadOnlyList<TareaVacunacionResumen>> Handle(
        ListarTareasPorGalponQuery request, CancellationToken cancellationToken)
    {
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        var lista = await tareas.ListarPorGalponAsync(galpon.Id, cancellationToken);
        var nombres = await programas.ObtenerNombresAsync(cancellationToken);
        return lista.OrderBy(t => t.FechaProgramada).Select(t => Mapear(t, nombres)).ToList();
    }

    internal static TareaVacunacionResumen Mapear(TareaVacunacion t, IReadOnlyDictionary<Guid, string>? nombres = null) => new(
        t.Id, t.GalponId, t.EdadDia, t.Vacuna, t.ModoAplicacion, t.FechaProgramada,
        t.Estado.ToString(), t.FechaAplicacion, t.AvesVacunadas,
        t.ObservacionesProgramadas, t.ObservacionesAplicacion, t.MotivoCancelacion,
        nombres is null ? null : nombres.GetValueOrDefault(t.ProgramaVacunacionId));
}

// Notificación (spec SP7): pendientes con FechaProgramada <= hoy + 7 días.
// VencidasYHoy (FechaProgramada <= hoy) no desaparece hasta completarse o
// cancelarse; Proximas es (hoy, hoy + 7]. El filtro global de tenant acota al
// cliente actual; el clienteId explícito es defensa en profundidad.
public sealed class ListarNotificacionVacunacionHandler(
    IRepositorioTareasVacunacion tareas, IRepositorioProgramasVacunacion programas, ICurrentUser usuario)
    : IRequestHandler<ListarNotificacionVacunacionQuery, NotificacionVacunacionResumen>
{
    public async Task<NotificacionVacunacionResumen> Handle(
        ListarNotificacionVacunacionQuery request, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var lista = await tareas.ListarNotificacionAsync(
            usuario.ClienteId ?? Guid.Empty, hoy, hoy.AddDays(7), cancellationToken);
        var nombres = await programas.ObtenerNombresAsync(cancellationToken);
        return new NotificacionVacunacionResumen(
            lista.Where(t => t.FechaProgramada <= hoy)
                .OrderBy(t => t.FechaProgramada).Select(t => ListarTareasPorGalponHandler.Mapear(t, nombres)).ToList(),
            lista.Where(t => t.FechaProgramada > hoy)
                .OrderBy(t => t.FechaProgramada).Select(t => ListarTareasPorGalponHandler.Mapear(t, nombres)).ToList());
    }
}
