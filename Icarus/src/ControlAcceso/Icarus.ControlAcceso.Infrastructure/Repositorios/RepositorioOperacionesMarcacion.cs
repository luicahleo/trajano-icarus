using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioOperacionesMarcacion(ControlAccesoDbContext db)
    : IRepositorioOperacionesMarcacion
{
    public void Agregar(OperacionMarcacion operacion) => db.OperacionesMarcacion.Add(operacion);

    public Task<OperacionMarcacion?> ObtenerPorClaveAsync(
        Guid clienteId, Guid claveIdempotencia, CancellationToken cancellationToken = default) =>
        db.OperacionesMarcacion
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ClienteId == clienteId && o.ClaveIdempotencia == claveIdempotencia,
                cancellationToken);

    public Task<OperacionMarcacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        db.OperacionesMarcacion.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
