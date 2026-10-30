using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioProgramasVacunacion(GestionAvicolaDbContext db) : IRepositorioProgramasVacunacion
{
    public void Agregar(ProgramaVacunacion programa) => db.ProgramasVacunacion.Add(programa);

    public void AgregarItem(ItemPlanVacunacion item) => db.ItemsPlanVacunacion.Add(item);

    // Tenant: el filtro global ya exige EstaActivo en programa e ítems.
    public async Task<ProgramaVacunacion?> ObtenerPublicadoPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id && p.Estado == EstadoProgramaVacunacion.Publicado, cancellationToken);

    public async Task<IReadOnlyList<ProgramaVacunacion>> ListarPublicadosAsync(
        CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion
            .Where(p => p.Estado == EstadoProgramaVacunacion.Publicado)
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

    // Gestor (spec 2026-10-30): se ignora el filtro de activos para ver los
    // publicados desactivados, pero nunca los borradores eliminados.
    public async Task<ProgramaVacunacion?> ObtenerParaGestionAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters().Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id
                && (p.EstaActivo || p.Estado == EstadoProgramaVacunacion.Publicado), cancellationToken);

    public async Task<IReadOnlyList<ProgramaVacunacion>> ListarParaGestionAsync(
        CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters().Include(p => p.Items)
            .Where(p => p.EstaActivo || p.Estado == EstadoProgramaVacunacion.Publicado)
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExisteNombreAsync(
        string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters()
            .AnyAsync(p => p.Nombre == nombre && (excluyendoId == null || p.Id != excluyendoId), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresAsync(
        CancellationToken cancellationToken = default) =>
        await db.ProgramasVacunacion.IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, cancellationToken);
}
