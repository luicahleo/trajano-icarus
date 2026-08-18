using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record DesactivarGranjaCommand(Guid GranjaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.granjas.desactivar", new Dictionary<string, DatoRegistroVuelo> { ["GalponesDesactivados"] = DatoRegistroVuelo.Entero });
}
