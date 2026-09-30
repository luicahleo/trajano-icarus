using Icarus.ControlAcceso.Application.Notificaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioNotificacionesInternasAcceso(ControlAccesoDbContext db)
    : INotificacionesInternasAcceso
{
    public void Agregar(NotificacionInternaAcceso notificacion) =>
        db.NotificacionesInternasAcceso.Add(notificacion);

    public Task<NotificacionInternaAcceso?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        db.NotificacionesInternasAcceso.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<NotificacionInternaAcceso?> ObtenerPorIncidenciaAsync(
        Guid incidenciaId, CancellationToken cancellationToken = default) =>
        db.NotificacionesInternasAcceso.SingleOrDefaultAsync(
            n => n.IncidenciaId == incidenciaId, cancellationToken);

    public async Task<IReadOnlyList<NotificacionInternaAcceso>> ListarAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        await db.NotificacionesInternasAcceso
            .Where(n => n.ClienteId == clienteId)
            .OrderByDescending(n => n.FechaUtc)
            .ToListAsync(cancellationToken);

    public Task<int> ContarNoLeidasAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        db.NotificacionesInternasAcceso
            .CountAsync(n => n.ClienteId == clienteId && !n.Leida, cancellationToken);
}
