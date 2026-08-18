using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
namespace Icarus.GestionAvicola.Infrastructure.Repositorios;
public sealed class RepositorioProduccion(GestionAvicolaDbContext db) : IRepositorioProduccion
{
    public void Agregar(RegistroProduccion r) => db.RegistrosProduccion.Add(r);
    public Task<RegistroProduccion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => db.RegistrosProduccion.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<RegistroProduccion>> ListarPorDiaAsync(Guid galponId, DateOnly fecha, CancellationToken ct = default) => await db.RegistrosProduccion.Where(x => x.GalponId == galponId && x.Fecha == fecha).OrderBy(x => x.Hora).ToListAsync(ct);
    public async Task<IReadOnlyList<RegistroProduccion>> ListarPorRangoAsync(Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken ct = default) => await db.RegistrosProduccion.Where(x => x.GalponId == galponId && x.Fecha >= desde && x.Fecha <= hasta).OrderBy(x => x.Fecha).ThenBy(x => x.Hora).ToListAsync(ct);
    public Task<RegistroProduccion?> ObtenerPorIdempotencyKeyAsync(Guid galponId, Guid key, CancellationToken ct = default) => db.RegistrosProduccion.SingleOrDefaultAsync(x => x.GalponId == galponId && x.IdempotencyKey == key, ct);
}
