using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Biometria;

// Puerto de cifrado de plantillas. La implementación concreta usa AES-GCM y
// claves administradas fuera de SQL y git (Infrastructure).
public interface IProtectorPlantillas
{
    PlantillaFacialProtegida Proteger(
        byte[] vector,
        string modeloFormato,
        Guid clienteId,
        Guid trabajadorId,
        int versionEnrolamiento,
        int versionClave = 1);

    byte[]? Recuperar(PlantillaFacialProtegida protegida);
}
