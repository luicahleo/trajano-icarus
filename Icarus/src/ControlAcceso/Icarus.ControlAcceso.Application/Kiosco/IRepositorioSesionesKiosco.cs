using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Kiosco;

public interface IRepositorioSesionesKiosco
{
    void Agregar(SesionKiosco sesion);

    Task<SesionKiosco?> ObtenerPorHashAsync(
        string hashCredencial,
        CancellationToken cancellationToken = default);

    Task<SesionKiosco?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Un único kiosco vigente por tenant: al activar se revocan las anteriores.
    Task RevocarVigentesDeClienteAsync(
        Guid clienteId,
        DateTimeOffset ahoraUtc,
        CancellationToken cancellationToken = default);
}
