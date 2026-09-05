using Icarus.GestionAvicola.Application.Notificaciones;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Repositorio de notificaciones internas (spec SP8): sin filtro de tenant en
// el DbContext porque el alcance incluye la bandeja global de CAISY, cuyo
// ClienteId queda nulo; cada método exige el alcance y solo devuelve filas de
// él.
public sealed class RepositorioNotificacionesInternas(GestionAvicolaDbContext db)
    : INotificacionesInternas
{
    public void Agregar(NotificacionInterna notificacion) =>
        db.NotificacionesInternas.Add(notificacion);

    public async Task<NotificacionInterna?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternas.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<NotificacionInterna>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternas
            .Where(n => n.ClienteId == clienteId)
            .ToListAsync(cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternas
            .Where(n => n.ClienteId == clienteId && !n.Leida)
            .CountAsync(cancellationToken);
}
