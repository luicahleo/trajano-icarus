using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class ConsultaPermisosActuales : IConsultaPermisosActuales
{
    private readonly ClientesDbContext _db;

    public ConsultaPermisosActuales(ClientesDbContext db) => _db = db;

    // Ignora los filtros globales y exige EstaActivo explícitamente, igual que
    // VerificadorEntitlement: un cliente suspendido o un trabajador
    // desactivado no tienen permisos que mostrar.
    public async Task<PermisosActuales> ObtenerAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default)
    {
        if (trabajadorId is { } id)
        {
            var contexto = await _db.Trabajadores.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.Id == id && t.ClienteId == clienteId && t.EstaActivo)
                .Join(_db.Clientes.IgnoreQueryFilters().AsNoTracking().Where(c => c.EstaActivo),
                    t => t.ClienteId, c => c.Id, (t, c) => new { t.Funcionalidades, c.ModulosHabilitados })
                .SingleOrDefaultAsync(cancellationToken);
            if (contexto is null)
                return new PermisosActuales([], []);
            var efectivas = contexto.Funcionalidades
                & FuncionalidadesTrabajador.Asignables
                & FuncionalidadesDe(contexto.ModulosHabilitados);
            return new PermisosActuales([], NombresFuncionalidades(efectivas));
        }

        var modulos = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId && c.EstaActivo)
            .Select(c => (Modulos?)c.ModulosHabilitados)
            .SingleOrDefaultAsync(cancellationToken);
        var habilitados = modulos ?? Modulos.Ninguno;
        return new PermisosActuales(NombresModulos(habilitados), NombresFuncionalidades(FuncionalidadesDe(habilitados)));
    }

    private static Funcionalidades FuncionalidadesDe(Modulos modulos)
    {
        var acumulado = Funcionalidades.Ninguno;
        foreach (var modulo in Enum.GetValues<Modulos>())
            if (modulo != Modulos.Ninguno && modulos.HasFlag(modulo))
                acumulado |= FuncionalidadesModulos.FuncionalidadesDelModulo(modulo);
        return acumulado;
    }

    private static IReadOnlyList<string> NombresModulos(Modulos modulos) =>
        Enum.GetValues<Modulos>()
            .Where(m => m != Modulos.Ninguno && modulos.HasFlag(m))
            .Select(m => m.ToString())
            .ToList();

    private static IReadOnlyList<string> NombresFuncionalidades(Funcionalidades funcionalidades) =>
        Enum.GetValues<Funcionalidades>()
            .Where(f => f != Funcionalidades.Ninguno && funcionalidades.HasFlag(f))
            .Select(f => f.ToString())
            .ToList();
}
