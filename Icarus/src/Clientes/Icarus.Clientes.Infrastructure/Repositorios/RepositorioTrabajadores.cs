using Icarus.Clientes.Application.Trabajadores;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Repositorios;

public sealed class RepositorioTrabajadores : IRepositorioTrabajadores
{
    private readonly ClientesDbContext _db;

    public RepositorioTrabajadores(ClientesDbContext db) => _db = db;

    public void Agregar(Trabajador trabajador) => _db.Trabajadores.Add(trabajador);

    public async Task<Trabajador?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Trabajadores.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TrabajadorResumen>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken cancellationToken = default)
    {
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Where(t => t.ClienteId == clienteId)
            .OrderBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
        return trabajadores
            .Select(t => new TrabajadorResumen(
                t.Id, t.Nombre, t.DocumentoIdentidad, t.Cargo, t.FechaIngreso, t.FechaCese,
                TextoFuncionalidades(t.Funcionalidades)))
            .ToList();
    }

    // Ignora los filtros globales: la unicidad del documento por cliente también
    // cubre a los desactivados (el soft delete no libera el documento).
    public async Task<bool> ExisteDocumentoAsync(
        Guid clienteId, string documentoIdentidad, CancellationToken cancellationToken = default) =>
        await _db.Trabajadores.IgnoreQueryFilters()
            .AnyAsync(t => t.ClienteId == clienteId && t.DocumentoIdentidad == documentoIdentidad,
                cancellationToken);

    private static IReadOnlyList<string> TextoFuncionalidades(Funcionalidades funcionalidades) =>
        funcionalidades == Funcionalidades.Ninguno
            ? []
            : funcionalidades.ToString().Split(", ", StringSplitOptions.RemoveEmptyEntries);
}
