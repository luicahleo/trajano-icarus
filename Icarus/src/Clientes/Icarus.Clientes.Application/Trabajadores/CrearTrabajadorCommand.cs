using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

// Gestión de trabajadores: Administrador, y Cliente sobre su propia empresa
// (spec). El filtro de tenant garantiza la segunda parte. Email y contrasena
// no se registran en logs ni en mensajes de error (anti-PII).
public sealed record CrearTrabajadorCommand(
    Guid ClienteId, string Nombre, string DocumentoIdentidad, string Cargo,
    DateOnly FechaIngreso, string Email, string Contrasena) : IRequest<Guid>;
