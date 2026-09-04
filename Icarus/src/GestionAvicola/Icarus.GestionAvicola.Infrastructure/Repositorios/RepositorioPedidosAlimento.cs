using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Repositorio de pedidos del tenant (spec SP8): las consultas pasan por el
// filtro del DbContext; el conteo bloqueable usa una consulta directa con
// UPDLOCK y semántica serializable para que dos envíos concurrentes del mismo
// cliente no superen el límite semanal.
public sealed class RepositorioPedidosAlimento(GestionAvicolaDbContext db)
    : IRepositorioPedidosAlimento
{
    public void Agregar(PedidoAlimento pedido) => db.PedidosAlimento.Add(pedido);

    public void AgregarDetalle(DetallePedidoAlimento detalle) =>
        db.Set<DetallePedidoAlimento>().Add(detalle);

    public async Task<PedidoAlimento?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento.Include(p => p.Detalles)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PedidoAlimento?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento
            .Include(p => p.Detalles)
            .Include(p => p.Historial)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PedidoAlimento>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento.Include(p => p.Detalles)
            .ToListAsync(cancellationToken);

    public async Task<int> ContarEnviadosEnSemanaBloqueandoAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento
            .FromSqlInterpolated(
                $@"SELECT * FROM gestion_avicola.pedidos_alimentos WITH (UPDLOCK, HOLDLOCK)
                   WHERE ClienteId = {clienteId} AND EstaActivo = 1
                     AND FechaPedido BETWEEN {desde} AND {hasta}")
            .CountAsync(cancellationToken);

    public async Task<ITransaccionPedidos> IniciarTransaccionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        return new TransaccionPedidos(transaccion);
    }

    // Envuelve la transacción de EF: Confirmar la cierra con commit y liberar
    // sin confirmar la revierte (el envío falla entero o no queda nada).
    private sealed class TransaccionPedidos(IDbContextTransaction transaccion)
        : ITransaccionPedidos
    {
        public async Task ConfirmarAsync(CancellationToken cancellationToken = default) =>
            await transaccion.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await transaccion.DisposeAsync();
    }
}
