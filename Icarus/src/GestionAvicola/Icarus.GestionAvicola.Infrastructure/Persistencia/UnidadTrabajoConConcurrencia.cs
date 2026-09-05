using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

// Decorador de la unidad de trabajo del módulo: traduce la pérdida de
// concurrencia optimista (rowversion) a un conflicto genérico para que los
// comandos mutables respondan 409 y no 500 (spec SP8). Mensaje genérico
// (anti-PII).
public sealed class UnidadTrabajoConConcurrencia(IUnidadTrabajoGestionAvicola interna)
    : IUnidadTrabajoGestionAvicola
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
