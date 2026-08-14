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
    // cliente suspendido pierde el acceso, igual que un trabajador desactivado.
    public async Task<bool> TieneFuncionalidadAsync(
        Guid clienteId, Guid? trabajadorId, Funcionalidades funcionalidad,
        CancellationToken cancellationToken = default)
    {
        if (trabajadorId is { } id)
        {
            // Rol Trabajador: solo sus funcionalidades asignadas.
            var asignadas = await _db.Trabajadores.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.Id == id && t.ClienteId == clienteId && t.EstaActivo)
                .Select(t => (Funcionalidades?)t.Funcionalidades)
                .SingleOrDefaultAsync(cancellationToken);
            return asignadas is { } funcionalidades && funcionalidades.HasFlag(funcionalidad);
        }

        // Rol Cliente: todas las funcionalidades de los módulos de su empresa.
        var modulos = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId && c.EstaActivo)
            .Select(c => (Modulos?)c.ModulosHabilitados)
            .SingleOrDefaultAsync(cancellationToken);
        return modulos is { } habilitados && habilitados.HasFlag(FuncionalidadesModulos.ModuloDe(funcionalidad));
    }
}
