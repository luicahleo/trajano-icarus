using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Catálogo global de precios de huevo (spec SP9): sin filtro de tenant; el
// acceso lo autoriza la política de CAISY, no el filtro del DbContext.
public sealed class RepositorioPublicacionesPreciosHuevo(GestionAvicolaDbContext db)
    : IRepositorioPublicacionesPreciosHuevo
{
    public void Agregar(PublicacionPrecioHuevo publicacion) =>
        db.PublicacionesPreciosHuevo.Add(publicacion);

    public void AgregarDetalle(DetallePrecioHuevo detalle) =>
        db.Set<DetallePrecioHuevo>().Add(detalle);

    public async Task<PublicacionPrecioHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.PublicacionesPreciosHuevo.Include(p => p.Detalles)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // Última Publicada con FechaVigencia <= fecha (spec SP9): la vigencia no
    // necesita procesos programados.
    public async Task<PublicacionPrecioHuevo?> ObtenerVigenteAsync(
        DateOnly fecha, CancellationToken cancellationToken = default) =>
        await db.PublicacionesPreciosHuevo.Include(p => p.Detalles)
            .Where(p => p.Estado == EstadoPublicacionPrecioHuevo.Publicada
                && p.FechaVigencia <= fecha)
            .OrderByDescending(p => p.FechaVigencia)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistePublicadaConVigenciaIgualAsync(
        DateOnly fechaVigencia, Guid? excluyendoId = null,
        CancellationToken cancellationToken = default) =>
        await db.PublicacionesPreciosHuevo
            .Where(p => p.Estado == EstadoPublicacionPrecioHuevo.Publicada
                && p.FechaVigencia == fechaVigencia
                && (excluyendoId == null || p.Id != excluyendoId))
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<PublicacionPrecioHuevo>> ListarHistorialAsync(
        CancellationToken cancellationToken = default) =>
        await db.PublicacionesPreciosHuevo.Include(p => p.Detalles)
            .OrderByDescending(p => p.FechaVigencia)
            .ThenByDescending(p => p.FechaNotificacion)
            .ToListAsync(cancellationToken);

    public async Task<ITransaccionPreciosHuevo> IniciarTransaccionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        return new TransaccionPreciosHuevo(transaccion);
    }

    // Envuelve la transacción de EF: mismo patrón que TransaccionPedidos en
    // RepositorioPedidosAlimento. Confirmar hace commit; no confirmar y
    // disponer revierte.
    private sealed class TransaccionPreciosHuevo(IDbContextTransaction transaccion)
        : ITransaccionPreciosHuevo
    {
        public async Task ConfirmarAsync(CancellationToken cancellationToken = default) =>
            await transaccion.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await transaccion.DisposeAsync();
    }
}
