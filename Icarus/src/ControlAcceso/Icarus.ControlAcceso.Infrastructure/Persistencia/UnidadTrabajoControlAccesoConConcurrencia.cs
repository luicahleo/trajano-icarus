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
        catch (DbUpdateException ex) when (EsConflictoDeUnicidad(ex))
        {
            // Carrera entre peticiones sobre un índice único (misma clave de
            // idempotencia o misma jornada): reintentar es seguro.
            throw new ConflictException("El registro cambió mientras se guardaba; reintente.");
        }
    }

    private static bool EsConflictoDeUnicidad(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual is Microsoft.Data.SqlClient.SqlException sql &&
                sql.Number is 2601 or 2627)
                return true;
        }

        return false;
    }
}
