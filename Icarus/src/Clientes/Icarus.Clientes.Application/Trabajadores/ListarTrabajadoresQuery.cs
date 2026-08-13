using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed record ListarTrabajadoresQuery(Guid ClienteId) : IRequest<IReadOnlyList<TrabajadorResumen>>;
