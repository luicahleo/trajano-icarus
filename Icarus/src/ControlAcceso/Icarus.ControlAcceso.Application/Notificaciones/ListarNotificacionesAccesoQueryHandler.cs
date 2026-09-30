using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.ControlAcceso.Application.Notificaciones;

public sealed class ListarNotificacionesAccesoQueryHandler(
    INotificacionesInternasAcceso notificaciones,
    ICurrentUser usuarioActual)
    : IRequestHandler<ListarNotificacionesAccesoQuery, IReadOnlyList<NotificacionAccesoResumen>>
{
    public async Task<IReadOnlyList<NotificacionAccesoResumen>> Handle(
        ListarNotificacionesAccesoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var items = await notificaciones.ListarAsync(clienteId, cancellationToken);
        return items
            .OrderByDescending(n => n.FechaUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificacionAccesoResumen(
                n.Id, n.IncidenciaId, "IncidenciaAcceso", n.FechaUtc, n.Leida))
            .ToList();
    }
}
