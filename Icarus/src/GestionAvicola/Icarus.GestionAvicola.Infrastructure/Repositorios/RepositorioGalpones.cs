using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioGalpones : IRepositorioGalpones
{
    private readonly GestionAvicolaDbContext _db;
    public RepositorioGalpones(GestionAvicolaDbContext db) => _db = db;
    public void Agregar(Galpon galpon) => _db.Galpones.Add(galpon);
    public Task<Galpon?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => _db.Galpones.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
    public async Task<IReadOnlyList<GalponResumen>> ListarPorGranjaAsync(Guid granjaId, CancellationToken cancellationToken = default) => await _db.Galpones.AsNoTracking().Where(g => g.GranjaId == granjaId).OrderBy(g => g.Numero).Select(g => new GalponResumen(g.Id, g.Numero, g.CapacidadMaxima, g.GallinasActuales, g.FechaNacimientoLote, g.Descripcion)).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Galpon>> ListarActivosDeGranjaAsync(Guid granjaId, CancellationToken cancellationToken = default) => await _db.Galpones.Where(g => g.GranjaId == granjaId).ToListAsync(cancellationToken);
    public Task<bool> ExisteNumeroAsync(Guid granjaId, string numero, CancellationToken cancellationToken = default) => _db.Galpones.IgnoreQueryFilters().AnyAsync(g => g.GranjaId == granjaId && g.Numero == numero, cancellationToken);
}
