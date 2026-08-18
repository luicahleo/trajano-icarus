using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class CrearGranjaHandler : IRequestHandler<CrearGranjaCommand, Guid>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly ICurrentUser _usuarioActual;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public CrearGranjaHandler(IRepositorioGranjas granjas, ICurrentUser usuarioActual, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _usuarioActual = usuarioActual;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task<Guid> Handle(CrearGranjaCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("Solo una cuenta de cliente puede registrar granjas.");
        var nombre = request.Nombre.Trim();
        if (await _granjas.ObtenerActivaDelTenantAsync(cancellationToken) is not null
            || await _granjas.ExisteNombreAsync(clienteId, nombre, cancellationToken))
            throw new ConflictException("No se pudo registrar la granja.");
        var granja = new Granja(clienteId, nombre);
        _granjas.Agregar(granja);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
        return granja.Id;
    }
}
