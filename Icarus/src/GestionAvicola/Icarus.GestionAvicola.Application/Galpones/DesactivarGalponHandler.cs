using Icarus.BuildingBlocks.Domain;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class DesactivarGalponHandler : IRequestHandler<DesactivarGalponCommand>
{
    private readonly IRepositorioGalpones _galpones; private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;
    public DesactivarGalponHandler(IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo) { _galpones = galpones; _unidadTrabajo = unidadTrabajo; }
    public async Task Handle(DesactivarGalponCommand request, CancellationToken cancellationToken) { var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken) ?? throw new NotFoundException("Galpon", request.GalponId); galpon.Desactivar(); await _unidadTrabajo.SaveChangesAsync(cancellationToken); }
}
