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

    public async Task<IReadOnlyList<DespachoHuevo>> ListarDelTenantAsync(
        CancellationToken cancellationToken = default) =>
        await db.DespachosHuevo.Include(d => d.Detalles).ToListAsync(cancellationToken);
}
