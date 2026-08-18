using Icarus.BuildingBlocks.Domain;
using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class ActualizarGalponHandler : IRequestHandler<ActualizarGalponCommand>
{
    private readonly IRepositorioGalpones _galpones; private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;
    public ActualizarGalponHandler(IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo) { _galpones = galpones; _unidadTrabajo = unidadTrabajo; }
    public async Task Handle(ActualizarGalponCommand request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        var numero = request.Numero.Trim();
        if (!string.Equals(galpon.Numero, numero, StringComparison.Ordinal)
            && await _galpones.ExisteNumeroAsync(galpon.GranjaId, numero, cancellationToken))
        {
            throw new ConflictException("No se pudo actualizar el galpón.");
        }

        galpon.ActualizarDatos(numero, request.Descripcion, request.CapacidadMaxima);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
