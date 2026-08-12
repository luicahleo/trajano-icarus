using MediatR;

namespace Icarus.Identity.Application.Sesiones;

public sealed record IniciarSesionCommand(string Email, string Contrasena) : IRequest<ResultadoSesion>;
