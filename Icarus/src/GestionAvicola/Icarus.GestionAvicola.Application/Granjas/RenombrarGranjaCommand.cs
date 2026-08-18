using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record RenombrarGranjaCommand(Guid GranjaId, string Nombre) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.granjas.renombrar", new Dictionary<string, DatoRegistroVuelo>());
}
