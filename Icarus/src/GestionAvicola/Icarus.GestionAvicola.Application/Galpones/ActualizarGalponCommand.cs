using Icarus.BuildingBlocks.Application.Observability;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed record ActualizarGalponCommand(Guid GalponId, string Numero, string? Descripcion, int CapacidadMaxima) : IRequest, IOperacionRegistrable
{ public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.galpones.actualizar", new Dictionary<string, DatoRegistroVuelo> { ["Numero"] = DatoRegistroVuelo.Texto, ["CapacidadMaxima"] = DatoRegistroVuelo.Entero }); }
