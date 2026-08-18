using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record CrearGranjaCommand(string Nombre) : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.granjas.crear", new Dictionary<string, DatoRegistroVuelo>());
}
