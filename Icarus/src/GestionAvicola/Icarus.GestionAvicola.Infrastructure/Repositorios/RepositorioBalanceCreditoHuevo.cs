using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Cálculo directo contra el DbContext (spec SP9): cruza DespachoHuevo y
// PedidoAlimento/RecepcionPedidoAlimento, dos agregados del mismo módulo. El
// cálculo usa los campos primitivos (CantidadAmarras, UnidadesSueltas,
// PrecioUnitarioCongelado) en vez de las propiedades calculadas del dominio
// (CantidadHuevos, Subtotal) porque estas últimas no garantizan traducirse a
// SQL de forma fiable. DetalleDespachoHuevo.HuevosPorAmarra es un const: EF
// Core lo traduce como literal en la proyección, no como acceso a miembro en
// tiempo de ejecución.
public sealed class RepositorioBalanceCreditoHuevo(GestionAvicolaDbContext db) : IRepositorioBalanceCreditoHuevo
{
    public async Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var fechaCorte = hoy.AddDays(-ReglasCreditoHuevo.DiasDisponibilidadCredito);

        var ingresos = await db.DespachosHuevo
            .Where(d => d.ClienteId == clienteId && d.Estado == EstadoDespachoHuevo.Recibido
                && d.FechaRecepcion != null && d.FechaRecepcion <= fechaCorte)
            .SelectMany(d => d.Detalles)
            .Where(det => det.PrecioUnitarioCongelado != null)
            .SumAsync(det =>
                (det.CantidadAmarras * DetalleDespachoHuevo.HuevosPorAmarra + det.UnidadesSueltas)
                    * det.PrecioUnitarioCongelado!.Value,
                cancellationToken);

        var recibidoReal = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.RecibidoConforme
                    || p.Estado == EstadoPedidoAlimento.RecibidoConDiferencias))
            .Select(p => p.Recepcion!.TotalRecibido)
            .SumAsync(cancellationToken);

        // Comprometido pendiente: pedidos ya enviados que todavía no llegaron
        // a recepción real, con su monto congelado al envío. Sin esto, dos
        // envíos concurrentes del mismo cliente (o varios envíos seguidos
        // antes de que CAISY reciba el primero) verían el mismo saldo y
        // ninguno dispararía la advertencia aunque juntos superen el
        // crédito disponible. EnviarPedidoAlimentoHandler ya serializa los
        // envíos concurrentes del mismo cliente con
        // ContarEnviadosEnSemanaBloqueandoAsync (UPDLOCK+HOLDLOCK) antes de
        // llegar a este cálculo, así que el segundo envío en llegar ya ve el
        // primero comprometido aquí — no hace falta un lock propio.
        var comprometidoPendiente = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.Solicitado
                    || p.Estado == EstadoPedidoAlimento.Aceptado
                    || p.Estado == EstadoPedidoAlimento.Despachado))
            .SelectMany(p => p.Detalles)
            .Where(d => d.SubtotalSolicitado != null)
            .SumAsync(d => d.SubtotalSolicitado!.Value, cancellationToken);

        return ingresos - recibidoReal - comprometidoPendiente;
    }
}
