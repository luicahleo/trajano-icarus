using Icarus.BuildingBlocks.Application.Observability;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed record DesactivarGalponCommand(Guid GalponId) : IRequest, IOperacionRegistrable
{ public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.galpones.desactivar", new Dictionary<string, DatoRegistroVuelo>()); }
