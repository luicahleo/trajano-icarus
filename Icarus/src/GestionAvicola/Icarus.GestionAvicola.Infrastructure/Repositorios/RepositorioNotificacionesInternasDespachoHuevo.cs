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

    public async Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .Where(n => n.ClienteId == clienteId)
            .ToListAsync(cancellationToken);

    public async Task<int> ContarNoLeidasAsync(
        Guid? clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasDespachoHuevo
            .CountAsync(n => n.ClienteId == clienteId && !n.Leida, cancellationToken);
}
