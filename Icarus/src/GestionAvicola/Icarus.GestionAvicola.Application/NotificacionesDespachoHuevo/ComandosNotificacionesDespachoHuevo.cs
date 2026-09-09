using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

public sealed record ListarNotificacionesDespachoHuevoQuery
    : IRequest<IReadOnlyList<NotificacionDespachoHuevoResumen>>;

public sealed record NotificacionDespachoHuevoResumen(
    Guid Id, string Tipo, Guid? DespachoHuevoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record ContarNotificacionesDespachoHuevoNoLeidasQuery : IRequest<int>;

public sealed record MarcarNotificacionDespachoHuevoLeidaCommand(Guid NotificacionId) : IRequest;

public sealed class ListarNotificacionesDespachoHuevoHandler(
    INotificacionesInternasDespachoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ListarNotificacionesDespachoHuevoQuery, IReadOnlyList<NotificacionDespachoHuevoResumen>>
{
    public async Task<IReadOnlyList<NotificacionDespachoHuevoResumen>> Handle(
        ListarNotificacionesDespachoHuevoQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarAsync(usuarioActual.ClienteId, cancellationToken))
            .OrderByDescending(n => n.FechaUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificacionDespachoHuevoResumen(
                n.Id, n.Tipo.ToString(), n.DespachoHuevoId, n.FechaUtc, n.Leida, n.Meta))
            .ToList();
}

public sealed class ContarNotificacionesDespachoHuevoNoLeidasHandler(
    INotificacionesInternasDespachoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ContarNotificacionesDespachoHuevoNoLeidasQuery, int>
{
    public Task<int> Handle(
        ContarNotificacionesDespachoHuevoNoLeidasQuery request, CancellationToken cancellationToken) =>
        repositorio.ContarNoLeidasAsync(usuarioActual.ClienteId, cancellationToken);
}

public sealed class MarcarNotificacionDespachoHuevoLeidaHandler(
    INotificacionesInternasDespachoHuevo repositorio,
    ICurrentUser usuarioActual,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<MarcarNotificacionDespachoHuevoLeidaCommand>
{
    public async Task Handle(MarcarNotificacionDespachoHuevoLeidaCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación", request.NotificacionId);
        if (notificacion.ClienteId != usuarioActual.ClienteId)
            throw new NotFoundException("Notificación", request.NotificacionId);
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        notificacion.MarcarLeida(actorId);
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
