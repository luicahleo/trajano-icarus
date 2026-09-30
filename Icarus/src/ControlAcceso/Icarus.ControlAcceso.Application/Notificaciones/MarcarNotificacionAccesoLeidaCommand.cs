using MediatR;

namespace Icarus.ControlAcceso.Application.Notificaciones;

public sealed record MarcarNotificacionAccesoLeidaCommand(Guid NotificacionId)
    : IRequest;
