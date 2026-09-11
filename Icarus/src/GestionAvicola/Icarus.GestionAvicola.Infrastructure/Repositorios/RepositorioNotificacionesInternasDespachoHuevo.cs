using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioNotificacionesInternasDespachoHuevo(GestionAvicolaDbContext db)
    : INotificacionesInternasDespachoHuevo
{
    public void Agregar(NotificacionInternaDespachoHuevo notificacion) =>
        db.NotificacionesInternasDespachoHuevo.Add(notificacion);

    public async Task<NotificacionInternaDespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    // El filtro de tipo se traduce a un IN sobre una columna int
    // (HasConversion<int> en la configuración de EF) y se aplica sobre un
    // conjunto ya acotado por el índice (ClienteId, FechaUtc): no justifica
    // un índice propio.
    public async Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .Where(n => n.ClienteId == clienteId && tiposVisibles.Contains(n.Tipo))
            .ToListAsync(cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        Guid? clienteId,
        IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
        CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .CountAsync(
                n => n.ClienteId == clienteId && !n.Leida && tiposVisibles.Contains(n.Tipo),
                cancellationToken);
}
