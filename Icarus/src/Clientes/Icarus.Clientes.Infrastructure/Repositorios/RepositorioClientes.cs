using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Repositorios;

public sealed class RepositorioClientes : IRepositorioClientes
{
    private readonly ClientesDbContext _db;

    public RepositorioClientes(ClientesDbContext db) => _db = db;

    public void Agregar(Cliente cliente) => _db.Clientes.Add(cliente);

    public async Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Clientes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Cliente?> ObtenerGestionablePorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await _db.Clientes.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClienteResumen>> ListarTodosAsync(CancellationToken cancellationToken = default)
    {
        var clientes = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(c => c.RazonSocial)
            .ToListAsync(cancellationToken);
        return clientes
            .Select(c => new ClienteResumen(
                c.Id, c.RazonSocial, c.IdentificadorFiscal, c.EstaActivo, TextoModulos(c.ModulosHabilitados)))
            .ToList();
    }

    public async Task<bool> ExisteIdentificadorFiscalAsync(
        string identificadorFiscal, CancellationToken cancellationToken = default) =>
        await _db.Clientes.IgnoreQueryFilters()
            .AnyAsync(c => c.IdentificadorFiscal == identificadorFiscal, cancellationToken);

    private static IReadOnlyList<string> TextoModulos(Modulos modulos) =>
        modulos == Modulos.Ninguno
            ? []
            : modulos.ToString().Split(", ", StringSplitOptions.RemoveEmptyEntries);
}
