using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioFlujosMarcacion(ControlAccesoDbContext db)
    : IRepositorioFlujosMarcacion
{
    public void Agregar(FlujoMarcacion flujo) => db.FlujosMarcacion.Add(flujo);

    public Task<FlujoMarcacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        db.FlujosMarcacion.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<FlujoMarcacion?> ObtenerPorIdConCapturasAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        db.FlujosMarcacion
            .Include(f => f.Capturas)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
}
