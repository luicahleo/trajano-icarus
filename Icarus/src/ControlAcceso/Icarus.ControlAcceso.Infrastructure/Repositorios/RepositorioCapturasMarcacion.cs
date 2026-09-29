using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioCapturasMarcacion(ControlAccesoDbContext db)
    : IRepositorioCapturasMarcacion
{
    public void Agregar(CapturaMarcacion captura, Guid flujoId)
    {
        db.CapturasMarcacion.Add(captura);
        db.Entry(captura).Property("FlujoMarcacionId").CurrentValue = flujoId;
    }

    public Task<int> ContarRechazosPorFlujoAsync(
        Guid flujoId, CancellationToken cancellationToken = default) =>
        db.CapturasMarcacion
            .Where(c => EF.Property<Guid>(c, "FlujoMarcacionId") == flujoId
                && c.Resultado == ResultadoCapturaMarcacion.Rechazada)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<CapturaMarcacion>> ListarRechazosPorFlujoAsync(
        Guid flujoId, CancellationToken cancellationToken = default) =>
        await db.CapturasMarcacion
            .Where(c => EF.Property<Guid>(c, "FlujoMarcacionId") == flujoId
                && c.Resultado == ResultadoCapturaMarcacion.Rechazada)
            .OrderBy(c => c.InstanteUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistePorClaveAsync(
        Guid claveCaptura, CancellationToken cancellationToken = default) =>
        db.CapturasMarcacion.AnyAsync(c => c.ClaveCaptura == claveCaptura, cancellationToken);
}
