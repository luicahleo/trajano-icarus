using MediatR;

namespace Icarus.ControlAcceso.Application.Notificaciones;

public sealed record ListarNotificacionesAccesoQuery
    : IRequest<IReadOnlyList<NotificacionAccesoResumen>>;
