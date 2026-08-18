using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
namespace Icarus.GestionAvicola.Infrastructure.Repositorios;
public sealed class RepositorioMortalidad(GestionAvicolaDbContext db) : IRepositorioMortalidad
{
    public void Agregar(RegistroMortalidad r) => db.RegistrosMortalidad.Add(r);
    public Task<RegistroMortalidad?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => db.RegistrosMortalidad.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<RegistroMortalidad>> ListarPorDiaAsync(Guid galponId, DateOnly fecha, CancellationToken ct = default) => await db.RegistrosMortalidad.Where(x => x.GalponId == galponId && x.Fecha == fecha).OrderBy(x => x.Hora).ToListAsync(ct);
    public async Task<IReadOnlyList<RegistroMortalidad>> ListarPorRangoAsync(Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken ct = default) => await db.RegistrosMortalidad.Where(x => x.GalponId == galponId && x.Fecha >= desde && x.Fecha <= hasta).OrderBy(x => x.Fecha).ThenBy(x => x.Hora).ToListAsync(ct);
    public Task<RegistroMortalidad?> ObtenerPorIdempotencyKeyAsync(Guid galponId, Guid key, CancellationToken ct = default) => db.RegistrosMortalidad.SingleOrDefaultAsync(x => x.GalponId == galponId && x.IdempotencyKey == key, ct);
}
