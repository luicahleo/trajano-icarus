using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record CrearGalponCommand(Guid GranjaId, string Numero, int CapacidadMaxima, int GallinasActuales, DateOnly FechaNacimientoLote, string? Descripcion) : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.galpones.crear", new Dictionary<string, DatoRegistroVuelo> { ["Numero"] = DatoRegistroVuelo.Texto, ["CapacidadMaxima"] = DatoRegistroVuelo.Entero, ["GallinasActuales"] = DatoRegistroVuelo.Entero });
}
