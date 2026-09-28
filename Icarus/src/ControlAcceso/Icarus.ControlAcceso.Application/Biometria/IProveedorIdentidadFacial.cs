using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Biometria;

// Contrato interno de biometría (spec: ARGOS solo procesa por petición, sin
// perfiles persistentes ni base de datos). El backend deriva tenant y
// candidatos; el proveedor devuelve únicamente una referencia del conjunto
// recibido. Ninguna muestra, vector ni referencia se registra en logs.
public interface IProveedorIdentidadFacial
{
    Task<ResultadoEnrolamiento> ExtraerAsync(
        MuestraFacial muestra, CancellationToken cancellationToken = default);

    Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default);
}

// Muestra temporal capturada. Nunca se persiste ni se escribe en logs.
public sealed record MuestraFacial(byte[] Contenido, string Formato);

// Vector descifrado en memoria para una sola petición. No se cachea.
public sealed record CandidatoFacial(
    Guid TrabajadorId,
    byte[] Vector,
    string ModeloFormato,
    int VersionModelo,
    int VersionEnrolamiento);
