using MediatR;

namespace Icarus.Identity.Application.Sesiones;

public sealed record RenovarSesionCommand(string RefreshToken) : IRequest<ResultadoSesion>;
