using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class CrearGalponHandler : IRequestHandler<CrearGalponCommand, Guid>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IRepositorioGalpones _galpones;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;
    public CrearGalponHandler(IRepositorioGranjas granjas, IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo) { _granjas = granjas; _galpones = galpones; _unidadTrabajo = unidadTrabajo; }
    public async Task<Guid> Handle(CrearGalponCommand request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);
        if (!granja.EstaActivo)
            throw new NotFoundException("Granja", request.GranjaId);
        var numero = request.Numero.Trim();
        if (await _galpones.ExisteNumeroAsync(granja.Id, numero, cancellationToken)) throw new ConflictException("No se pudo registrar el galpón.");
        var galpon = new Galpon(granja.Id, granja.ClienteId, numero, request.CapacidadMaxima, request.GallinasActuales, request.FechaNacimientoLote, request.Descripcion);
        _galpones.Agregar(galpon); await _unidadTrabajo.SaveChangesAsync(cancellationToken); return galpon.Id;
    }
}
