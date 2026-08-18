using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioGranjas : IRepositorioGranjas
{
    private readonly GestionAvicolaDbContext _db;
    public RepositorioGranjas(GestionAvicolaDbContext db) => _db = db;
    public void Agregar(Granja granja) => _db.Granjas.Add(granja);
    public Task<Granja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => _db.Granjas.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
    public Task<Granja?> ObtenerActivaDelTenantAsync(CancellationToken cancellationToken = default) => _db.Granjas.FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<GranjaResumen>> ListarDelTenantAsync(CancellationToken cancellationToken = default) => await _db.Granjas.AsNoTracking().OrderBy(g => g.Nombre).Select(g => new GranjaResumen(g.Id, g.Nombre)).ToListAsync(cancellationToken);
    public Task<bool> ExisteNombreAsync(Guid clienteId, string nombre, CancellationToken cancellationToken = default) => _db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.ClienteId == clienteId && g.Nombre == nombre, cancellationToken);
}
