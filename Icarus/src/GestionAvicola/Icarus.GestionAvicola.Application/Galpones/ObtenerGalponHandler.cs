using Icarus.BuildingBlocks.Domain;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class ObtenerGalponHandler : IRequestHandler<ObtenerGalponQuery, GalponResumen>
{
    private readonly IRepositorioGalpones _galpones; public ObtenerGalponHandler(IRepositorioGalpones galpones) => _galpones = galpones;
    public async Task<GalponResumen> Handle(ObtenerGalponQuery request, CancellationToken cancellationToken) { var g = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken) ?? throw new NotFoundException("Galpon", request.GalponId); return new GalponResumen(g.Id, g.Numero, g.CapacidadMaxima, g.GallinasActuales, g.FechaNacimientoLote, g.Descripcion); }
}
