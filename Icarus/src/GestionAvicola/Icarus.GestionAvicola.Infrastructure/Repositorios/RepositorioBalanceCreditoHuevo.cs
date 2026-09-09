using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Cálculo directo contra el DbContext (spec SP9): cruza DespachoHuevo y
// PedidoAlimento/RecepcionPedidoAlimento, dos agregados del mismo módulo. El
// cálculo usa los campos primitivos (CantidadAmarras, UnidadesSueltas,
// PrecioProductorCongelado) en vez de las propiedades calculadas del dominio
// (CantidadHuevos, Subtotal) porque estas últimas no garantizan traducirse a
// SQL de forma fiable.
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
            .Where(det => det.PrecioProductorCongelado != null)
            .SumAsync(det =>
                (det.CantidadAmarras * 180 + det.UnidadesSueltas) * det.PrecioProductorCongelado!.Value,
                cancellationToken);

        var egresos = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.RecibidoConforme
                    || p.Estado == EstadoPedidoAlimento.RecibidoConDiferencias))
            .Select(p => p.Recepcion!.TotalRecibido)
            .SumAsync(cancellationToken);

        return ingresos - egresos;
    }
}
