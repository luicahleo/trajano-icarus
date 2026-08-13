using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class VerificadorEntitlement : IVerificadorEntitlement
{
    private readonly ClientesDbContext _db;

    public VerificadorEntitlement(ClientesDbContext db) => _db = db;

    // Ignora los filtros globales y exige EstaActivo explícitamente: un
    // cliente suspendido pierde el acceso a sus módulos.
    public async Task<bool> TieneModuloHabilitadoAsync(
        Guid clienteId, Modulos modulo, CancellationToken cancellationToken = default)
    {
        var modulos = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId && c.EstaActivo)
            .Select(c => (Modulos?)c.ModulosHabilitados)
            .SingleOrDefaultAsync(cancellationToken);
        return modulos is { } habilitados && habilitados.HasFlag(modulo);
    }
}
