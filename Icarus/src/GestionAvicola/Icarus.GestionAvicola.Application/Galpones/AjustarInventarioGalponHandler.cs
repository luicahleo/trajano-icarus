using Icarus.BuildingBlocks.Domain;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class AjustarInventarioGalponHandler : IRequestHandler<AjustarInventarioGalponCommand>
{
    private readonly IRepositorioGalpones _galpones; private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;
    public AjustarInventarioGalponHandler(IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo) { _galpones = galpones; _unidadTrabajo = unidadTrabajo; }
    public async Task Handle(AjustarInventarioGalponCommand request, CancellationToken cancellationToken) { var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken) ?? throw new NotFoundException("Galpon", request.GalponId); galpon.AjustarInventarioGallinas(request.GallinasActuales); await _unidadTrabajo.SaveChangesAsync(cancellationToken); }
}
