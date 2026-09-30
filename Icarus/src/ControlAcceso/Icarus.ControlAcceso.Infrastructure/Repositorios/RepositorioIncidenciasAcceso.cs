using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioIncidenciasAcceso(ControlAccesoDbContext db)
    : IRepositorioIncidenciasAcceso
{
    public void Agregar(IncidenciaAcceso incidencia) => db.IncidenciasAcceso.Add(incidencia);

    public Task<IncidenciaAcceso?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        db.IncidenciasAcceso.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<IncidenciaAcceso?> ObtenerPorFlujoIdAsync(
        Guid flujoId, CancellationToken cancellationToken = default) =>
        db.IncidenciasAcceso.FirstOrDefaultAsync(i => i.FlujoMarcacionId == flujoId, cancellationToken);

    public Task<IReadOnlyList<IncidenciaAcceso>> ListarPendientesAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        ListarAsync(clienteId, EstadoIncidenciaAcceso.Pendiente, cancellationToken);

    public async Task<IReadOnlyList<IncidenciaAcceso>> ListarAsync(
        Guid clienteId,
        EstadoIncidenciaAcceso? estado = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = db.IncidenciasAcceso.Where(i => i.ClienteId == clienteId);
        if (estado.HasValue)
            consulta = consulta.Where(i => i.Estado == estado.Value);

        return await consulta
            .OrderByDescending(i => i.TercerRechazoUtc)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);
    }
}
