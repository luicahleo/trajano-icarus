using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioJornadasAcceso(ControlAccesoDbContext db)
    : IRepositorioJornadasAcceso
{
    public void Agregar(JornadaAcceso jornada) => db.JornadasAcceso.Add(jornada);

    public async Task<JornadaAcceso?> ObtenerAsync(
        Guid clienteId,
        Guid trabajadorId,
        DateOnly fechaBoliviana,
        CancellationToken cancellationToken = default) =>
        await db.JornadasAcceso
            .Include(j => j.Marcaciones.OrderBy(m => m.InstanteUtc))
            .Include(j => j.Revisiones)
            .ThenInclude(r => r.ValoresEfectivos)
            .FirstOrDefaultAsync(
                j => j.ClienteId == clienteId && j.TrabajadorId == trabajadorId && j.FechaBoliviana == fechaBoliviana,
                cancellationToken);

    public async Task<JornadaAcceso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await db.JornadasAcceso
            .Include(j => j.Marcaciones)
            .Include(j => j.Revisiones)
            .ThenInclude(r => r.ValoresEfectivos)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<Pagina<JornadaAcceso>> ListarAsync(
        Guid clienteId,
        Guid? trabajadorId,
        DateOnly? desde,
        DateOnly? hasta,
        PeticionPaginada paginacion,
        CancellationToken cancellationToken = default)
    {
        var consulta = db.JornadasAcceso
            .AsNoTracking()
            .Where(j => j.ClienteId == clienteId);

        if (trabajadorId.HasValue)
            consulta = consulta.Where(j => j.TrabajadorId == trabajadorId.Value);
        if (desde.HasValue)
            consulta = consulta.Where(j => j.FechaBoliviana >= desde.Value);
        if (hasta.HasValue)
            consulta = consulta.Where(j => j.FechaBoliviana <= hasta.Value);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(j => j.FechaBoliviana)
            .Skip(paginacion.Salto)
            .Take(paginacion.TamanoNormalizado)
            .ToListAsync(cancellationToken);

        return new Pagina<JornadaAcceso>(items, total, paginacion.PaginaNormalizada, paginacion.TamanoNormalizado);
    }
}
