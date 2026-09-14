using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Repositorio de despachos de huevo del tenant (spec SP9): las consultas pasan
// por el filtro del DbContext (tenant y EstaActivo), igual que los pedidos de
// alimento. Sin conteo bloqueable ni transacción explícita: Despachar no
// compite por un cupo compartido, así que SaveChangesAsync alcanza.
public sealed class RepositorioDespachosHuevo(GestionAvicolaDbContext db) : IRepositorioDespachosHuevo
{
    public void Agregar(DespachoHuevo despacho) => db.DespachosHuevo.Add(despacho);

    public void AgregarDetalle(DetalleDespachoHuevo detalle) =>
        db.Set<DetalleDespachoHuevo>().Add(detalle);

    public async Task<DespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).Include(d => d.DocumentoNota)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<DespachoHuevo?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).Include(d => d.Historial)
            .Include(d => d.DocumentoNota)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<DespachoHuevo> Items, int Total)> ListarPaginadoTenantAsync(
        Guid? granjaId, EstadoDespachoHuevo? estado, DateOnly? desde, DateOnly? hasta,
        Guid? creadoPorTrabajadorId, int? numero,
        int saltar, int tomar, CancellationToken cancellationToken = default)
    {
        var consulta = db.DespachosHuevo.Include(d => d.Detalles).AsNoTracking();
        if (granjaId is { } granja)
            consulta = consulta.Where(d => d.GranjaId == granja);
        if (estado is { } e)
            consulta = consulta.Where(d => d.Estado == e);
        if (desde is { } desdeValor)
            consulta = consulta.Where(d => d.FechaDespacho >= desdeValor);
        if (hasta is { } hastaValor)
            consulta = consulta.Where(d => d.FechaDespacho <= hastaValor);
        if (creadoPorTrabajadorId is { } trabajador)
            consulta = consulta.Where(d => d.CreadoPorTrabajadorId == trabajador);
        if (numero is { } n)
            consulta = consulta.Where(d => d.Numero == n);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(d => d.Numero)
            .Skip(saltar).Take(tomar)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<DespachoHuevo> Items, int Total, IReadOnlyDictionary<Guid, string> Granjas)>
        ListarPaginadoCaisyAsync(
            EstadoDespachoHuevo? estado, string? granja, DateOnly? desde, DateOnly? hasta,
            int? numero, int saltar, int tomar, CancellationToken cancellationToken = default)
    {
        // CAISY ve el despacho desde que el tenant lo envía (spec 2026-09-14).
        // Va antes del filtro por estado a propósito: si estuviera dentro del
        // if, un ?estado=Borrador volvería a exponer la lista completa.
        var consulta = db.DespachosHuevo.Include(d => d.Detalles).AsNoTracking()
            .Where(d => d.Estado != EstadoDespachoHuevo.Borrador);
        if (estado is { } e)
            consulta = consulta.Where(d => d.Estado == e);
        if (!string.IsNullOrWhiteSpace(granja))
        {
            var nombre = granja.Trim();
            consulta = consulta.Where(d =>
                db.Granjas.Any(g => g.Id == d.GranjaId && g.Nombre.Contains(nombre)));
        }
        if (desde is { } desdeValor)
            consulta = consulta.Where(d => d.FechaDespacho >= desdeValor);
        if (hasta is { } hastaValor)
            consulta = consulta.Where(d => d.FechaDespacho <= hastaValor);
        if (numero is { } n)
            consulta = consulta.Where(d => d.Numero == n);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(d => d.Numero)
            .Skip(saltar).Take(tomar)
            .ToListAsync(cancellationToken);
        var idsGranjas = items.Select(d => d.GranjaId).Distinct().ToList();
        var granjas = await db.Granjas.IgnoreQueryFilters()
            .Where(g => idsGranjas.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Nombre, cancellationToken);
        return (items, total, granjas);
    }

    public async Task<IReadOnlyList<DespachoHuevo>> ListarRecibidosPorPublicacionAsync(
        Guid publicacionId, CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo
            .Include(d => d.Detalles)
            .Where(d => d.Estado == EstadoDespachoHuevo.Recibido
                && d.Detalles.Any(det => det.PublicacionPrecioHuevoId == publicacionId))
            .ToListAsync(cancellationToken);
}
