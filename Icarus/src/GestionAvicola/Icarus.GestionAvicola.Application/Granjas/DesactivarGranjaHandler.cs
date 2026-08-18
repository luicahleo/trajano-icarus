using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class DesactivarGranjaHandler : IRequestHandler<DesactivarGranjaCommand>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IRepositorioGalpones _galpones;
    private readonly IRegistroVuelo _registroVuelo;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public DesactivarGranjaHandler(IRepositorioGranjas granjas, IRepositorioGalpones galpones, IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _galpones = galpones;
        _registroVuelo = registroVuelo;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(DesactivarGranjaCommand request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);
        var galpones = await _galpones.ListarActivosDeGranjaAsync(granja.Id, cancellationToken);
        foreach (var galpon in galpones)
            galpon.Desactivar();
        if (galpones.Count > 0)
            _registroVuelo.Decidir("avicola.granjas.desactivar", "cascada_galpones", "aplicada", new Dictionary<string, object?> { ["GalponesDesactivados"] = galpones.Count });
        granja.Desactivar();
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
