using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

// Soft delete (glosario): desactivar nunca borra la fila.
public sealed record DesactivarTrabajadorCommand(Guid TrabajadorId) : IRequest;
