using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class RenombrarGranjaHandler : IRequestHandler<RenombrarGranjaCommand>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public RenombrarGranjaHandler(IRepositorioGranjas granjas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(RenombrarGranjaCommand request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);
        var nombre = request.Nombre.Trim();
        if (!string.Equals(granja.Nombre, nombre, StringComparison.Ordinal)
            && await _granjas.ExisteNombreAsync(granja.ClienteId, nombre, cancellationToken))
            throw new ConflictException("No se pudo renombrar la granja.");
        granja.Renombrar(nombre);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
