using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed record ListarClientesQuery : IRequest<IReadOnlyList<ClienteResumen>>;
