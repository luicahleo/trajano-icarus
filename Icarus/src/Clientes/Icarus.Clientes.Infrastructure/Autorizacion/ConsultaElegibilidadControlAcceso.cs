using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class ConsultaElegibilidadControlAcceso : IConsultaElegibilidadControlAcceso
{
    private readonly ClientesDbContext _db;

    public ConsultaElegibilidadControlAcceso(ClientesDbContext db) => _db = db;

    // Ignora filtros globales y exige condiciones explícitas: un cliente
    // suspendido o un trabajador desactivado/cesado no es elegible.
    public async Task<ElegibilidadControlAcceso> EvaluarAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default)
    {
        var cliente = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId)
            .Select(c => new { c.EstaActivo, c.ModulosHabilitados })
            .SingleOrDefaultAsync(cancellationToken);

        if (cliente is null)
            return new ElegibilidadControlAcceso(false, false, false, false, null);

        var moduloHabilitado = cliente.ModulosHabilitados.HasFlag(Modulos.ControlAcceso);

        if (trabajadorId is null)
            return new ElegibilidadControlAcceso(true, cliente.EstaActivo, moduloHabilitado, false, null);

        var trabajador = await _db.Trabajadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Id == trabajadorId && t.ClienteId == clienteId)
            .Select(t => new { t.EstaActivo, t.FechaCese })
            .SingleOrDefaultAsync(cancellationToken);

        if (trabajador is null)
            return new ElegibilidadControlAcceso(false, cliente.EstaActivo, moduloHabilitado, false, null);

        return new ElegibilidadControlAcceso(
            true,
            cliente.EstaActivo,
            moduloHabilitado,
            trabajador.EstaActivo,
            trabajador.FechaCese);
    }
}
