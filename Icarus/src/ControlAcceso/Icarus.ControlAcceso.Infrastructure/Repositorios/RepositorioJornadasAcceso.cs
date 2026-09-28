using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
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

    public async Task<JornadaAcceso> ObtenerOCrearAsync(
        Guid clienteId,
        Guid trabajadorId,
        DateOnly fechaBoliviana,
        CancellationToken cancellationToken = default)
    {
        var existente = await ObtenerAsync(clienteId, trabajadorId, fechaBoliviana, cancellationToken);
        if (existente is not null)
            return existente;

        var nueva = new JornadaAcceso(clienteId, trabajadorId, fechaBoliviana);
        db.JornadasAcceso.Add(nueva);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return nueva;
        }
        catch (DbUpdateException)
        {
            // Otra petición (kiosco o manual) creó la jornada primero:
            // se descarta la copia propia y se devuelve la ganadora.
            db.Entry(nueva).State = EntityState.Detached;
            var ganadora = await ObtenerAsync(clienteId, trabajadorId, fechaBoliviana, cancellationToken);
            if (ganadora is null)
                throw new ConflictException("El registro cambió mientras se guardaba; reintente.");
            return ganadora;
        }
    }

    public async Task<JornadaAcceso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await db.JornadasAcceso
            .Include(j => j.Marcaciones.OrderBy(m => m.InstanteUtc))
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
            .Include(j => j.Marcaciones)
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
