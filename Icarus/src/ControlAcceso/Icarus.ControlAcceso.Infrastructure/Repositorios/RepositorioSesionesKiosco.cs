using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioSesionesKiosco(ControlAccesoDbContext db) : IRepositorioSesionesKiosco
{
    public void Agregar(SesionKiosco sesion) => db.SesionesKiosco.Add(sesion);

    public Task<SesionKiosco?> ObtenerPorHashAsync(
        string hashCredencial,
        CancellationToken cancellationToken = default) =>
        db.SesionesKiosco.FirstOrDefaultAsync(
            s => s.HashCredencial == hashCredencial, cancellationToken);

    public Task<SesionKiosco?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        db.SesionesKiosco.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task RevocarVigentesDeClienteAsync(
        Guid clienteId,
        DateTimeOffset ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var vigentes = await db.SesionesKiosco
            .Where(s => s.ClienteId == clienteId && s.EstaActiva)
            .ToListAsync(cancellationToken);

        foreach (var sesion in vigentes)
            sesion.Revocar(ahoraUtc);
    }
}
