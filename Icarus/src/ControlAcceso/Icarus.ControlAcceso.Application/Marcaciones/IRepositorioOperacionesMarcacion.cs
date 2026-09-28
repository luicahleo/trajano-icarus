using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public interface IRepositorioOperacionesMarcacion
{
    void Agregar(OperacionMarcacion operacion);

    Task<OperacionMarcacion?> ObtenerPorClaveAsync(
        Guid clienteId, Guid claveIdempotencia, CancellationToken cancellationToken = default);

    Task<OperacionMarcacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);
}
