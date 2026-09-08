using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PedidosAlimento;

// Recibo imprimible (spec SP8D "Recibo PDF en Trajano.GestorCaisy"): consulta
// de solo lectura sobre datos ya persistidos del despacho — número y fecha de
// nota, líneas con precio y subtotal congelados, totales. No toca el
// agregado ni la máquina de estados; se puede pedir cualquier cantidad de
// veces sobre un pedido con entrega registrada.
public sealed record ObtenerReciboPedidoPdfQuery(Guid PedidoId) : IRequest<byte[]>;

// Contrato técnico del renderizado: la implementación (QuestPDF u otra) vive
// en Infraestructura, igual que IAlmacenDocumentosPedido.
public interface IReciboPedidoRenderer
{
    Task<byte[]> RenderizarAsync(PedidoAlimento pedido, CancellationToken cancellationToken = default);
}

public sealed class ObtenerReciboPedidoPdfHandler(
    IRepositorioPedidosAlimento repositorio, IReciboPedidoRenderer renderer)
    : IRequestHandler<ObtenerReciboPedidoPdfQuery, byte[]>
{
    public async Task<byte[]> Handle(ObtenerReciboPedidoPdfQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerConHistorialAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Entrega is null)
            throw new NotFoundException("Entrega de pedido", request.PedidoId);
        return await renderer.RenderizarAsync(pedido, cancellationToken);
    }
}
