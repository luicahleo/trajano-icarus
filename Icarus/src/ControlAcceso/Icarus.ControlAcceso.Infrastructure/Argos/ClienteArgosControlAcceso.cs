using Icarus.ControlAcceso.Application.Biometria;
using Microsoft.Extensions.Options;

namespace Icarus.ControlAcceso.Infrastructure.Argos;

// Scaffold fail-closed para registrar el cliente HTTP antes de implementar el
// contrato en la tarea 8. No envía muestras ni registra credenciales.
public sealed class ClienteArgosControlAcceso : IProveedorIdentidadFacial
{
    public ClienteArgosControlAcceso(
        HttpClient httpClient,
        IOptions<OpcionesArgosControlAcceso> opciones)
    {
        _ = httpClient;
        _ = opciones;
    }

    public Task<ResultadoEnrolamiento> ExtraerAsync(
        MuestraFacial muestra, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoEnrolamiento.Rechazado("proveedor_no_disponible"));

    public Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible"));
}
