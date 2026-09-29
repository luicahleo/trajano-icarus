using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public interface IRepositorioIncidenciasAcceso
{
    void Agregar(IncidenciaAcceso incidencia);

    Task<IncidenciaAcceso?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IncidenciaAcceso?> ObtenerPorFlujoIdAsync(
        Guid flujoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidenciaAcceso>> ListarPendientesAsync(
        Guid clienteId, CancellationToken cancellationToken = default);
}
