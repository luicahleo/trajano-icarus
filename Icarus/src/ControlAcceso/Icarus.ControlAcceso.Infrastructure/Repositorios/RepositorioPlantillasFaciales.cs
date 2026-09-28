using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioPlantillasFaciales(ControlAccesoDbContext db)
    : IRepositorioPlantillasFaciales
{
    public async Task<PlantillaFacialProtegida?> ObtenerActivaAsync(
        Guid clienteId,
        Guid trabajadorId,
        CancellationToken cancellationToken = default) =>
        await db.PlantillasFaciales
            .AsNoTracking()
            .Where(p => p.ClienteId == clienteId && p.TrabajadorId == trabajadorId && p.EstaActivo)
            .OrderByDescending(p => p.VersionEnrolamiento)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PlantillaFacialProtegida?> ObtenerPorVersionAsync(
        Guid clienteId,
        Guid trabajadorId,
        int versionEnrolamiento,
        CancellationToken cancellationToken = default) =>
        await db.PlantillasFaciales
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ClienteId == clienteId && p.TrabajadorId == trabajadorId
                                              && p.VersionEnrolamiento == versionEnrolamiento,
                cancellationToken);

    public Task GuardarAsync(
        PlantillaFacialProtegida plantilla,
        CancellationToken cancellationToken = default)
    {
        db.PlantillasFaciales.Add(plantilla);
        return Task.CompletedTask;
    }

    public async Task RevocarActivaAsync(
        Guid clienteId,
        Guid trabajadorId,
        CancellationToken cancellationToken = default)
    {
        var activa = await db.PlantillasFaciales
            .Where(p => p.ClienteId == clienteId && p.TrabajadorId == trabajadorId && p.EstaActivo)
            .OrderByDescending(p => p.VersionEnrolamiento)
            .FirstOrDefaultAsync(cancellationToken);

        activa?.Revocar();
    }
}
