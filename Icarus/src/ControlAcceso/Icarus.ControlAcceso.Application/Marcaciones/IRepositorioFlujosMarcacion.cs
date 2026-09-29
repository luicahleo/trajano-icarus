using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public interface IRepositorioFlujosMarcacion
{
    void Agregar(FlujoMarcacion flujo);

    Task<FlujoMarcacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<FlujoMarcacion?> ObtenerPorIdConCapturasAsync(
        Guid id, CancellationToken cancellationToken = default);
}
