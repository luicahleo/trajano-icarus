using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Recibo imprimible (spec SP9): consulta de solo lectura sobre datos ya
// persistidos, solo disponible una vez confirmada la recepción. No toca el
// agregado; se puede pedir cualquier cantidad de veces.
public sealed record ObtenerReciboDespachoHuevoPdfQuery(Guid DespachoId) : IRequest<byte[]>;

public interface IReciboDespachoHuevoRenderer
{
    Task<byte[]> RenderizarAsync(DespachoHuevo despacho, CancellationToken cancellationToken = default);
}

public sealed class ObtenerReciboDespachoHuevoPdfHandler(
    IRepositorioDespachosHuevo repositorio, IReciboDespachoHuevoRenderer renderer)
    : IRequestHandler<ObtenerReciboDespachoHuevoPdfQuery, byte[]>
{
    public async Task<byte[]> Handle(ObtenerReciboDespachoHuevoPdfQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Recibido)
            throw new NotFoundException("Recepción de despacho", request.DespachoId);
        return await renderer.RenderizarAsync(despacho, cancellationToken);
    }
}
