using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class UnidadTrabajoControlAccesoConConcurrencia(IUnidadTrabajoControlAcceso interna)
    : IUnidadTrabajoControlAcceso
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await interna.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El registro cambió mientras se guardaba; reintente.");
        }
    }
}
