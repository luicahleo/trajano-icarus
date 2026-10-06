using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed record ListarNombresTrabajadoresQuery(Guid ClienteId)
    : IRequest<IReadOnlyList<TrabajadorNombreResumen>>;

public sealed record TrabajadorNombreResumen(Guid Id, string Nombre);
