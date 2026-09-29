using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public interface IRepositorioCapturasMarcacion
{
    void Agregar(CapturaMarcacion captura, Guid flujoId);

    Task<int> ContarRechazosPorFlujoAsync(
        Guid flujoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapturaMarcacion>> ListarRechazosPorFlujoAsync(
        Guid flujoId, CancellationToken cancellationToken = default);

    Task<bool> ExistePorClaveAsync(
        Guid claveCaptura, CancellationToken cancellationToken = default);
}
