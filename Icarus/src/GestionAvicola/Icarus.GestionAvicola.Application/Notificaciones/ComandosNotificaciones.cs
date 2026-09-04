using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Notificaciones;

// Consulta y marcado de notificaciones del alcance propio (spec SP8). El
// alcance se toma de la cuenta: un tenant ve su bandeja y una cuenta CAISY
// (sin tenant) ve la global. Un cruce de alcance responde 404 genérico.
public sealed record ListarNotificacionesQuery
    : IRequest<IReadOnlyList<NotificacionResumen>>;

public sealed record NotificacionResumen(
    Guid Id, string Tipo, Guid PedidoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record ContarNotificacionesNoLeidasQuery : IRequest<int>;

public sealed record MarcarNotificacionLeidaCommand(Guid NotificacionId)
    : IRequest;

public sealed class ListarNotificacionesHandler(
    INotificacionesInternas repositorio,
    ICurrentUser usuarioActual)
    : IRequestHandler<ListarNotificacionesQuery, IReadOnlyList<NotificacionResumen>>
{
    public async Task<IReadOnlyList<NotificacionResumen>> Handle(
        ListarNotificacionesQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarAsync(usuarioActual.ClienteId, cancellationToken))
            .OrderByDescending(n => n.FechaUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificacionResumen(
                n.Id, n.Tipo.ToString(), n.PedidoId, n.FechaUtc, n.Leida, n.Meta))
            .ToList();
}

public sealed class ContarNotificacionesNoLeidasHandler(
    INotificacionesInternas repositorio,
    ICurrentUser usuarioActual)
    : IRequestHandler<ContarNotificacionesNoLeidasQuery, int>
{
    public Task<int> Handle(
        ContarNotificacionesNoLeidasQuery request, CancellationToken cancellationToken) =>
        repositorio.ContarNoLeidasAsync(usuarioActual.ClienteId, cancellationToken);
}

public sealed class MarcarNotificacionLeidaHandler(
    INotificacionesInternas repositorio,
    ICurrentUser usuarioActual,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<MarcarNotificacionLeidaCommand>
{
    public async Task Handle(MarcarNotificacionLeidaCommand request, CancellationToken cancellationToken)
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
