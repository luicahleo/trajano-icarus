using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class EstadoCliente : IClienteActivo
{
    private readonly ClientesDbContext _db;

    public EstadoCliente(ClientesDbContext db) => _db = db;

    public async Task<bool> EstaActivoAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId)
            .Select(c => (bool?)c.EstaActivo)
            .SingleOrDefaultAsync(cancellationToken) == true;
}
