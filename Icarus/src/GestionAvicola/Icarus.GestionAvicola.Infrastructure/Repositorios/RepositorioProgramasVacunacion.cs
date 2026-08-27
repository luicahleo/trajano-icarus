using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioProgramasVacunacion(GestionAvicolaDbContext db) : IRepositorioProgramasVacunacion
{
    public void Agregar(ProgramaVacunacion programa) => db.ProgramasVacunacion.Add(programa);

    public async Task<ProgramaVacunacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // Rol de plataforma (spec SP7): el Administrador gestiona el catálogo
    // completo, incluidos los inactivos.
    public async Task<ProgramaVacunacion?> ObtenerPorIdIncluyendoInactivosAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters().Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<bool> ExisteNombreAsync(
        string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters()
            .AnyAsync(p => p.Nombre == nombre && (excluyendoId == null || p.Id != excluyendoId), cancellationToken);

    public async Task<IReadOnlyList<ProgramaVacunacion>> ListarAsync(
        bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var consulta = incluirInactivos
            ? db.ProgramasVacunacion.IgnoreQueryFilters()
            : db.ProgramasVacunacion;
        return await consulta.OrderBy(p => p.Nombre).ToListAsync(cancellationToken);
    }
}
