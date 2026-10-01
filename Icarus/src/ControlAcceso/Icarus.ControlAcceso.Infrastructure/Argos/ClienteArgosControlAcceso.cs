using System.Net;
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
    private const string RutaIdentificaciones = "/api/v2/control-acceso/identificaciones";
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
        try
        {
            var solicitud = new SolicitudExtraccion(
                "trajano-icarus-control-acceso",
                Guid.Empty,
                Convert.ToBase64String(muestra.Contenido),
                muestra.Formato);

            using var respuesta = await _httpClient.PostAsJsonAsync(
                RutaExtracciones, solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
                return ResultadoEnrolamiento.Rechazado(await MotivoErrorAsync(respuesta, cancellationToken));

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaExtraccion>(
                cancellationToken: cancellationToken);
            if (cuerpo is not { Exitoso: true, PadAprobado: true } ||
                cuerpo.Vector is not { Length: > 0 } ||
                string.IsNullOrWhiteSpace(cuerpo.ModeloFormato) ||
                cuerpo.VersionModelo <= 0)
                return ResultadoEnrolamiento.Rechazado(MapCodigo(cuerpo?.Codigo));

            var vector = MemoryMarshal.AsBytes(cuerpo.Vector.AsSpan()).ToArray();
            return ResultadoEnrolamiento.Exito(vector, cuerpo.ModeloFormato, cuerpo.VersionModelo);
        }
        catch (JsonException)
        {
            return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");
        }
        catch (HttpRequestException)
        {
            return ResultadoEnrolamiento.Rechazado("proveedor_no_disponible");
        }
    }

    public async Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solicitud = new SolicitudIdentificacion(
                "trajano-icarus-control-acceso",
                Guid.Empty,
                Convert.ToBase64String(muestra.Contenido),
                muestra.Formato,
                candidatos.FirstOrDefault()?.ModeloFormato ?? "arcface-cosine-512",
                1,
                candidatos.Select(c => new CandidatoIdentificacion(
                    c.TrabajadorId.ToString(),
                    MemoryMarshal.Cast<byte, float>(c.Vector.AsSpan()).ToArray().Select(v => (double)v).ToArray(),
                    c.ModeloFormato,
                    c.VersionEnrolamiento)).ToArray());

            using var respuesta = await _httpClient.PostAsJsonAsync(
                RutaIdentificaciones, solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
                return ResultadoIdentificacionFacial.SinCoincidencia(await MotivoErrorAsync(respuesta, cancellationToken));

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIdentificacion>(
                cancellationToken: cancellationToken);
            if (cuerpo is { Identificado: true } && Guid.TryParse(cuerpo.TrabajadorId, out var trabajadorId))
                return ResultadoIdentificacionFacial.Coincide(trabajadorId);

            return ResultadoIdentificacionFacial.SinCoincidencia(MapCodigo(cuerpo?.Codigo));
        }
        catch (JsonException)
        {
            return ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible");
        }
        catch (HttpRequestException)
        {
            return ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible");
        }
    }

    private static async Task<string> MotivoErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        if (respuesta.StatusCode is not (HttpStatusCode.UnprocessableEntity or HttpStatusCode.InternalServerError))
            return "proveedor_no_disponible";

        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(cancellationToken: cancellationToken);
            return MapCodigo(error?.Codigo);
        }
        catch (JsonException)
        {
            return "proveedor_no_disponible";
        }
    }

    private static string MapCodigo(string? codigo) => codigo switch
    {
        "sin_rostro" => "sin_rostro",
        "varios_rostros" => "varios_rostros",
        "pad_fallido" => "pad_fallido",
        "extraccion_fallida" => "extraccion_fallida",
        "formato_invalido" => "formato_invalido",
        "imagen_muy_grande" => "imagen_muy_grande",
        "ambigua" => "ambigua",
        "modelo_incompatible" => "modelo_incompatible",
        "sin_coincidencia" => "sin_coincidencia",
        "sin_candidatos" => "sin_candidatos",
        _ => "proveedor_no_disponible"
    };

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
        [property: JsonPropertyName("pad_aprobado")] bool PadAprobado,
        [property: JsonPropertyName("codigo")] string? Codigo);

    private sealed record SolicitudIdentificacion(
        [property: JsonPropertyName("aplicacion")] string Aplicacion,
        [property: JsonPropertyName("tenant_id")] Guid TenantId,
        [property: JsonPropertyName("imagen")] string Imagen,
        [property: JsonPropertyName("formato")] string Formato,
        [property: JsonPropertyName("modelo_formato_esperado")] string ModeloFormatoEsperado,
        [property: JsonPropertyName("version_modelo_esperada")] int VersionModeloEsperada,
        [property: JsonPropertyName("candidatos")] IReadOnlyList<CandidatoIdentificacion> Candidatos);

    private sealed record CandidatoIdentificacion(
        [property: JsonPropertyName("trabajador_id")] string TrabajadorId,
        [property: JsonPropertyName("vector")] double[] Vector,
        [property: JsonPropertyName("modelo_formato")] string ModeloFormato,
        [property: JsonPropertyName("version_enrolamiento")] int VersionEnrolamiento);

    private sealed record RespuestaIdentificacion(
        [property: JsonPropertyName("identificado")] bool Identificado,
        [property: JsonPropertyName("trabajador_id")] string? TrabajadorId,
        [property: JsonPropertyName("codigo")] string? Codigo);

    private sealed record RespuestaError([property: JsonPropertyName("codigo")] string? Codigo);
}
