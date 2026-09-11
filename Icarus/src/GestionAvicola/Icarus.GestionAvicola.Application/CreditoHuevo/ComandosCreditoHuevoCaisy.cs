using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Vista de CAISY del crédito del cliente de un pedido (spec
// 2026-09-11-credito-huevo-vista-caisy-design). El alcance es el pedido, no
// el cliente: el ClienteId se deriva del pedido y nunca viaja como
// parámetro, así que no hay enumeración posible de clientes desde la API.
//
// Sin gate de rol dentro del handler, a diferencia de
// ObtenerBalanceCreditoHuevoHandler: la política del grupo
// /pedidos-alimento-caisy exige rol GestorCaisy más la funcionalidad
// GestorPedidoAlimento, mientras la política del tenant no distingue
// Cliente de Trabajador — de ahí que solo ese otro handler necesite el if.
public sealed record ObtenerCreditoHuevoDePedidoCaisyQuery(Guid PedidoId)
    : IRequest<CreditoHuevoDePedidoCaisy>;

// Tres cifras en vez de una: SaldoDisponible ya tiene descontado este
// pedido cuando está comprometido, así que quien lea solo ese número está
// viendo el saldo DESPUÉS del pedido, no antes de decidir.
public sealed record CreditoHuevoDePedidoCaisy(
    decimal SaldoDisponible,
    decimal MontoDelPedido,
    decimal SaldoSinEstePedido,
    bool PedidoComputadoEnElSaldo,
    IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);

public sealed class ObtenerCreditoHuevoDePedidoCaisyHandler(
    IRepositorioPedidosAlimento repositorioPedidos,
    IRepositorioBalanceCreditoHuevo repositorioCredito)
    : IRequestHandler<ObtenerCreditoHuevoDePedidoCaisyQuery, CreditoHuevoDePedidoCaisy>
{
    public async Task<CreditoHuevoDePedidoCaisy> Handle(
        ObtenerCreditoHuevoDePedidoCaisyQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorioPedidos.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        var saldo = await repositorioCredito.ObtenerSaldoDisponibleAsync(
            pedido.ClienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        var ajustes = await repositorioCredito.ObtenerAjustesAsync(
            pedido.ClienteId, cancellationToken);
        var monto = MontoQueElPedidoAportaAlSaldo(pedido);
        return new CreditoHuevoDePedidoCaisy(
            saldo, monto ?? 0m, saldo + (monto ?? 0m), monto is not null, ajustes);
    }

    // Cuánto pesa este pedido en el saldo, según su estado; null cuando no
    // pesa. Los dos componentes que lo incluyen se RESTAN en
    // RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync, así que el
    // saldo sin el pedido es siempre saldo + monto.
    private static decimal? MontoQueElPedidoAportaAlSaldo(PedidoAlimento pedido) =>
        pedido.Estado switch
        {
            // comprometidoPendiente suma los subtotales NO NULOS. No se usa
            // pedido.TotalSolicitado porque esa propiedad devuelve null si
            // algún subtotal es nulo, y daría un número distinto del que el
            // saldo realmente descontó.
            EstadoPedidoAlimento.Solicitado
                or EstadoPedidoAlimento.Aceptado
                or EstadoPedidoAlimento.Despachado =>
                pedido.Detalles
                    .Where(d => d.SubtotalSolicitado is not null)
                    .Sum(d => d.SubtotalSolicitado!.Value),
            // recibidoReal usa el snapshot Recepcion.TotalRecibido: el
            // pedido ya consumió crédito real y dejó de estar comprometido.
            EstadoPedidoAlimento.RecibidoConforme
                or EstadoPedidoAlimento.RecibidoConDiferencias =>
                pedido.Recepcion?.TotalRecibido ?? 0m,
            // Borrador y Rechazado no entran en ningún componente.
            _ => null,
        };
}
