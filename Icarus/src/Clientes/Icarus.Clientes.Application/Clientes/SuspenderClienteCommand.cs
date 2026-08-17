using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.Clientes.Application.Clientes;

public sealed record SuspenderClienteCommand(Guid ClienteId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "clientes.suspender_alta_incompleta", new Dictionary<string, DatoRegistroVuelo>());
}
