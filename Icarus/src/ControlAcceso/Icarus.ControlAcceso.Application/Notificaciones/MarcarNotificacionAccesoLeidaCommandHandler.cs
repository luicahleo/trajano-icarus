using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Persistencia;
using MediatR;

namespace Icarus.ControlAcceso.Application.Notificaciones;

public sealed class MarcarNotificacionAccesoLeidaCommandHandler(
    INotificacionesInternasAcceso notificaciones,
    ICurrentUser usuarioActual,
    IUnidadTrabajoControlAcceso unidadTrabajo)
    : IRequestHandler<MarcarNotificacionAccesoLeidaCommand>
{
    public async Task Handle(
        MarcarNotificacionAccesoLeidaCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await notificaciones.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación", request.NotificacionId);

        if (notificacion.ClienteId != usuarioActual.ClienteId)
            throw new NotFoundException("Notificación", request.NotificacionId);

        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        notificacion.MarcarLeida(actorId);
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
