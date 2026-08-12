using MediatR;

namespace Icarus.Identity.Application.Usuarios;

// Sistema cerrado: solo el Administrador da de alta cuentas (endpoint con
// política, task 5). No hay registro público.
public sealed record CrearUsuarioCommand(
    string Email, string Contrasena, string Rol, Guid? ClienteId, Guid? TrabajadorId) : IRequest<Guid>;
