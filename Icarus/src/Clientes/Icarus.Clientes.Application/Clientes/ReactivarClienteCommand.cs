using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed record ReactivarClienteCommand(Guid ClienteId) : IRequest;
