using Icarus.GestionAvicola.Application.BalanceAlimentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Consulta SQL canónica del balance (spec SP8C): una única fuente de verdad
// que suma, por tipo, los equivalentes realmente recibidos en los estados
// recibidos (5 = RecibidoConforme, 6 = RecibidoConDiferencias) multiplicados
// por el PrecioFinalPor40Kg congelado al envío (columna del detalle del
// pedido). La conversión de presentación vive acá y replica la del dominio:
// bolsa = 1 equivalente por unidad, granel = 25 por tonelada. Ni el precio
// vigente posterior ni el total informado de la nota participan del cálculo.
public sealed class RepositorioBalanceAlimentos(GestionAvicolaDbContext db)
    : IRepositorioBalanceAlimentos
{
    public async Task<IReadOnlyList<LineaBalanceAlimentos>> ObtenerAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        var filas = await db.Database
            .SqlQuery<LineaBalanceSql>($"""
                SELECT dp.TipoAlimento AS TipoAlimento,
                       SUM(dr.CantidadRecibida *
                           CASE dp.Presentacion WHEN 1 THEN 25 ELSE 1 END) AS EquivalentesRecibidos,
                       COUNT(*) AS PedidosRecibidos,
                       SUM(dr.CantidadRecibida *
                           CASE dp.Presentacion WHEN 1 THEN 25 ELSE 1 END
                           * dp.PrecioFinalPor40Kg) AS Gasto
                FROM gestion_avicola.pedidos_alimentos p
                JOIN gestion_avicola.recepciones_pedidos_alimentos r
                    ON r.PedidoAlimentoId = p.Id AND p.Estado IN (5, 6)
                JOIN gestion_avicola.detalles_recepciones_pedidos_alimentos dr
                    ON dr.RecepcionPedidoAlimentoId = r.Id
                JOIN gestion_avicola.detalles_pedidos_alimentos dp
                    ON dp.PedidoAlimentoId = p.Id AND dp.TipoAlimento = dr.TipoAlimento
                WHERE p.ClienteId = {clienteId}
                  AND p.EstaActivo = 1
                  AND p.FechaPedido BETWEEN {desde} AND {hasta}
                GROUP BY dp.TipoAlimento
                ORDER BY dp.TipoAlimento
                """)
            .ToListAsync(cancellationToken);
        return filas
            .Select(f => new LineaBalanceAlimentos(
                ((TipoAlimento)f.TipoAlimento).ToString(),
                f.EquivalentesRecibidos, f.PedidosRecibidos, f.Gasto))
            .ToList();
    }

    // Fila cruda del SQL: los nombres coinciden con los alias del SELECT y EF
    // los asigna por reflexión al materializar (no hay asignación en código).
#pragma warning disable S3459, S1144
    private sealed class LineaBalanceSql
    {
        public int TipoAlimento { get; set; }
        public int EquivalentesRecibidos { get; set; }
        public int PedidosRecibidos { get; set; }
        public decimal Gasto { get; set; }
    }
#pragma warning restore S3459, S1144
}
