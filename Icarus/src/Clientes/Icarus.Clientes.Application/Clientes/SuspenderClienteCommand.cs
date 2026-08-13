using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed record SuspenderClienteCommand(Guid ClienteId) : IRequest;
