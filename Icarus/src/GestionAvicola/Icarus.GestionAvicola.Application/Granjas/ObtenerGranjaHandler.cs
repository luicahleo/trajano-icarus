using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class ObtenerGranjaHandler : IRequestHandler<ObtenerGranjaQuery, GranjaResumen>
{
    private readonly IRepositorioGranjas _granjas;
    public ObtenerGranjaHandler(IRepositorioGranjas granjas) => _granjas = granjas;
    public async Task<GranjaResumen> Handle(ObtenerGranjaQuery request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);
        return new GranjaResumen(granja.Id, granja.Nombre);
    }
}
