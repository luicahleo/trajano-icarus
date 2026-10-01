using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Icarus.ControlAcceso.Application.Biometria;
using Microsoft.Extensions.Options;

namespace Icarus.ControlAcceso.Infrastructure.Argos;

public sealed class ClienteArgosControlAcceso : IProveedorIdentidadFacial
{
    private const string RutaExtracciones = "/api/v2/control-acceso/extracciones";
    private readonly HttpClient _httpClient;

    public ClienteArgosControlAcceso(
        HttpClient httpClient,
        IOptions<OpcionesArgosControlAcceso> opciones)
    {
        _httpClient = httpClient;
        var configuracion = opciones.Value;
        _httpClient.BaseAddress = new Uri(configuracion.Url, UriKind.Absolute);
        _httpClient.Timeout = configuracion.Timeout;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuracion.ApiKey);
    }

    public async Task<ResultadoEnrolamiento> ExtraerAsync(
        MuestraFacial muestra,
        CancellationToken cancellationToken = default)
    {
        var solicitud = new SolicitudExtraccion(
            "trajano-icarus-control-acceso",
            Guid.Empty,
            Convert.ToBase64String(muestra.Contenido),
            muestra.Formato);

        using var respuesta = await _httpClient.PostAsJsonAsync(
            RutaExtracciones, solicitud, cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
            return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");

        try
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaExtraccion>(
                cancellationToken: cancellationToken);
            if (cuerpo is not { Exitoso: true, PadAprobado: true } ||
                cuerpo.Vector is not { Length: > 0 } ||
                string.IsNullOrWhiteSpace(cuerpo.ModeloFormato) ||
                cuerpo.VersionModelo <= 0)
                return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");

            var vector = MemoryMarshal.AsBytes(cuerpo.Vector.AsSpan()).ToArray();
            return ResultadoEnrolamiento.Exito(vector, cuerpo.ModeloFormato, cuerpo.VersionModelo);
        }
        catch (JsonException)
        {
            return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");
        }
    }

    public Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible"));

    private sealed record SolicitudExtraccion(
        [property: JsonPropertyName("aplicacion")] string Aplicacion,
        [property: JsonPropertyName("tenant_id")] Guid TenantId,
        [property: JsonPropertyName("imagen")] string Imagen,
        [property: JsonPropertyName("formato")] string Formato);

    private sealed record RespuestaExtraccion(
        [property: JsonPropertyName("exitoso")] bool Exitoso,
        [property: JsonPropertyName("vector")] float[]? Vector,
        [property: JsonPropertyName("modelo_formato")] string? ModeloFormato,
        [property: JsonPropertyName("version_modelo")] int VersionModelo,
        [property: JsonPropertyName("pad_aprobado")] bool PadAprobado);
}
