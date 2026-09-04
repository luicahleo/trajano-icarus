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

    public void AgregarDocumentoNota(DocumentoNotaEntrega documento) =>
        db.Set<DocumentoNotaEntrega>().Add(documento);

    public async Task<PedidoAlimento?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento.Include(p => p.Detalles)
            .Include(p => p.Entrega)
            .ThenInclude(e => e!.Documentos)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PedidoAlimento?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento
            .Include(p => p.Detalles)
            .Include(p => p.Historial)
            .Include(p => p.Entrega)
            .ThenInclude(e => e!.Lineas)
            .Include(p => p.Entrega)
            .ThenInclude(e => e!.Documentos)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PedidoAlimento>> ListarAsync(
        CancellationToken cancellationToken = default) =>
        await db.PedidosAlimento.Include(p => p.Detalles)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<PedidoAlimento> Items, int Total)> ListarPaginadoCaisyAsync(
        EstadoPedidoAlimento? estado, PresentacionAlimento? presentacion,
        int saltar, int tomar, CancellationToken cancellationToken = default)
    {
        var consulta = db.PedidosAlimento.Include(p => p.Detalles).AsNoTracking();
        if (estado is { } e)
            consulta = consulta.Where(p => p.Estado == e);
        if (presentacion is { } pr)
            consulta = consulta.Where(p => p.Detalles.Any(d => d.Presentacion == pr));
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(p => p.FechaPedido)
            .ThenByDescending(p => p.Id)
            .Skip(saltar)
            .Take(tomar)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> ContarEnviadosEnSemanaBloqueandoAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default) =>
        // Cuentan los pedidos que hayan salido del borrador y no estén
        // borrados (spec SP8): Estado 0 es Borrador, así la devolución libera
        // el cupo temporalmente y el reenvío del mismo pedido no lo consume
        // otra vez (cuenta una sola vez por pedido y semana). UPDLOCK con
        // semántica serializable bloquea el rango hasta el fin de la
        // transacción del envío.
        db.PedidosAlimento
            .FromSqlInterpolated(
                $@"SELECT * FROM gestion_avicola.pedidos_alimentos WITH (UPDLOCK, HOLDLOCK)
                   WHERE ClienteId = {clienteId} AND EstaActivo = 1 AND Estado <> 0
                     AND FechaPedido BETWEEN {desde} AND {hasta}")
            .CountAsync(cancellationToken);

    public Task<int> ContarEnviadosEnSemanaAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default) =>
        db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId && p.EstaActivo
                && p.Estado != EstadoPedidoAlimento.Borrador
                && p.FechaPedido >= desde && p.FechaPedido <= hasta)
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
