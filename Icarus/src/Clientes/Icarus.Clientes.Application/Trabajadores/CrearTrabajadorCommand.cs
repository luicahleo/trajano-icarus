using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

// Gestión de trabajadores: Administrador, y Cliente sobre su propia empresa
// (spec). El filtro de tenant garantiza la segunda parte.
public sealed record CrearTrabajadorCommand(
    Guid ClienteId, string Nombre, string DocumentoIdentidad, string Cargo, DateOnly FechaIngreso)
    : IRequest<Guid>;
