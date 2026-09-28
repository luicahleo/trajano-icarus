using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Biometria;

public interface IRepositorioPlantillasFaciales
{
    Task<PlantillaFacialProtegida?> ObtenerActivaAsync(
        Guid clienteId,
        Guid trabajadorId,
        CancellationToken cancellationToken = default);

    Task<PlantillaFacialProtegida?> ObtenerPorVersionAsync(
        Guid clienteId,
        Guid trabajadorId,
        int versionEnrolamiento,
        CancellationToken cancellationToken = default);

    Task GuardarAsync(
        PlantillaFacialProtegida plantilla,
        CancellationToken cancellationToken = default);

    Task RevocarActivaAsync(
        Guid clienteId,
        Guid trabajadorId,
        CancellationToken cancellationToken = default);
}
