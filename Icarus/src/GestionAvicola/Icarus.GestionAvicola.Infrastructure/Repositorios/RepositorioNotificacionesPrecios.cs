using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Catálogo global de precios (spec SP8): sin filtro de tenant; el acceso lo
// autoriza la política de CAISY, no el filtro del DbContext.
public sealed class RepositorioNotificacionesPrecios(GestionAvicolaDbContext db)
    : IRepositorioNotificacionesPrecios
{
    public void Agregar(NotificacionPreciosAlimentos notificacion) =>
        db.NotificacionesPreciosAlimentos.Add(notificacion);

    public void AgregarDetalle(DetallePrecioAlimento detalle) =>
        db.Set<DetallePrecioAlimento>().Add(detalle);

    public async Task<NotificacionPreciosAlimentos?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.NotificacionesPreciosAlimentos.Include(n => n.Detalles)
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    // Última Publicada con VigenteDesde <= fecha (spec SP8): la vigencia no
    // necesita procesos programados.
    public async Task<NotificacionPreciosAlimentos?> ObtenerVigenteAsync(
        DateOnly fecha, CancellationToken cancellationToken = default) =>
        await db.NotificacionesPreciosAlimentos.Include(n => n.Detalles)
            .Where(n => n.Estado == EstadoNotificacionPreciosAlimentos.Publicada
                && n.VigenteDesde <= fecha)
            .OrderByDescending(n => n.VigenteDesde)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistePublicadaConVigenciaIgualAsync(
        DateOnly vigenteDesde, Guid? excluyendoId = null,
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesPreciosAlimentos
            .Where(n => n.Estado == EstadoNotificacionPreciosAlimentos.Publicada
                && n.VigenteDesde == vigenteDesde
                && (excluyendoId == null || n.Id != excluyendoId))
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificacionPreciosAlimentos>> ListarHistorialAsync(
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesPreciosAlimentos.Include(n => n.Detalles)
            .OrderByDescending(n => n.VigenteDesde)
            .ThenByDescending(n => n.FechaDocumento)
            .ToListAsync(cancellationToken);
}
