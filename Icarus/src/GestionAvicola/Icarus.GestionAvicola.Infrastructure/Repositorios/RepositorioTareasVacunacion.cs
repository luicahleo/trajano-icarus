using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioTareasVacunacion(GestionAvicolaDbContext db) : IRepositorioTareasVacunacion
{
    public void Agregar(TareaVacunacion tarea) => db.TareasVacunacion.Add(tarea);

    public async Task<TareaVacunacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TareaVacunacion>> ListarPorGalponAsync(
        Guid galponId, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion.Where(t => t.GalponId == galponId)
            .OrderBy(t => t.FechaProgramada).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TareaVacunacion>> ListarNotificacionAsync(
        Guid clienteId, DateOnly hoy, DateOnly hasta, CancellationToken cancellationToken = default) =>
        await db.TareasVacunacion
            .Where(t => t.ClienteId == clienteId
                && t.Estado == EstadoTareaVacunacion.Pendiente && t.FechaProgramada <= hasta)
            .OrderBy(t => t.FechaProgramada).ToListAsync(cancellationToken);

    // Soft delete vía el agregado (tracked): el historial completado/cancelado
    // no se toca (spec SP7). El filtro global ya excluye las desactivadas.
    public async Task<int> DesactivarPendientesDeGalponAsync(
        Guid galponId, CancellationToken cancellationToken = default)
    {
        var pendientes = await db.TareasVacunacion
            .Where(t => t.GalponId == galponId && t.Estado == EstadoTareaVacunacion.Pendiente)
            .ToListAsync(cancellationToken);
        foreach (var tarea in pendientes)
            tarea.Desactivar();
        return pendientes.Count;
    }
}
