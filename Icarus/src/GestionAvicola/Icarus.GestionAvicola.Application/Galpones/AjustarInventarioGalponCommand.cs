using Icarus.BuildingBlocks.Application.Observability;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed record AjustarInventarioGalponCommand(Guid GalponId, int GallinasActuales) : IRequest, IOperacionRegistrable
{ public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.galpones.ajustar-inventario", new Dictionary<string, DatoRegistroVuelo> { ["GallinasActuales"] = DatoRegistroVuelo.Entero }); }
