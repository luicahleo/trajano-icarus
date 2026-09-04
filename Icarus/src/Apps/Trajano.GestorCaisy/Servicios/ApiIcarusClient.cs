using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Trajano.GestorCaisy.Servicios;

public sealed class ApiIcarusClient : IApiIcarusClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string CookieRefresh = "icarus_refresh";

    private readonly HttpClient _http;
    private readonly string? _baseUrl;
    private readonly IHttpContextAccessor _contexto;
    private readonly ISesionCaisyActual _sesion;
    private readonly ILogger<ApiIcarusClient> _registro;

    public ApiIcarusClient(
        HttpClient http, IConfiguration configuracion,
        IHttpContextAccessor contexto, ISesionCaisyActual sesion,
        ILogger<ApiIcarusClient> registro)
    {
        _http = http;
        _baseUrl = configuracion["ApiIcarus:BaseUrl"];
        _contexto = contexto;
        _sesion = sesion;
        _registro = registro;
    }

    public async Task<SesionApi> IniciarSesionAsync(
        string correo, string contrasena, CancellationToken token = default)
    {
        // Renovable en falso: renovar una renovación solo multiplicaría los
        // intentos con credenciales ya rechazadas.
        using var respuesta = await EnviarAsync(
            _ => PeticionJson(HttpMethod.Post, "identidad/sesion", null,
                new { correo, contrasena }),
            renovable: false, token);
        await AsegurarExitoAsync(respuesta, token);
        var sesion = await respuesta.Content
            .ReadFromJsonAsync<SesionApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta de sesión ilegible");
        var refresh = LeerCookieRefresh(respuesta)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "La sesión no trajo renovación");
        return sesion with { RefreshToken = refresh };
    }

    public async Task<IReadOnlyList<NotificacionPreciosResumenApi>> ListarNotificacionesAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, "precios-alimentos/", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content
                .ReadFromJsonAsync<IReadOnlyList<NotificacionPreciosResumenApi>>(Json, token)
            ?? [];
    }

    public async Task<NotificacionPreciosDetalleApi> ObtenerNotificacionAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, $"precios-alimentos/{id}", accessToken),
            token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<NotificacionPreciosDetalleApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<Guid> ImportarPdfAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default)
    {
        // El PDF se copia a memoria (tope de 20 MB del lado de la API) para
        // que la renovación de sesión pueda reenviar la misma carga.
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, token);
        var bytes = memoria.ToArray();
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionMultipart(bytes, nombreArchivo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var borrador = await respuesta.Content.ReadFromJsonAsync<BorradorImportadoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta de importación ilegible");
        return borrador.Id;
    }

    public async Task ActualizarBorradorAsync(
        ComandoActualizarBorradorApi comando, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Put,
                $"precios-alimentos/{comando.NotificacionId}", accessToken, comando), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task PublicarAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-alimentos/{id}/publicar", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task AnularFuturaAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-alimentos/{id}/anular", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<Stream> DescargarDocumentoOriginalAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, $"precios-alimentos/{id}/documento-original", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }

    // Núcleo: envía con el access token actual; ante un 401 renueva la sesión
    // una vez y reintenta con el token fresco. Rutas de sesión nunca se
    // renuevan (evita bucles con credenciales inválidas).
    private async Task<HttpResponseMessage> EnviarConSesionAsync(
        Func<string?, HttpRequestMessage> crearPeticion, CancellationToken token) =>
        await EnviarAsync(crearPeticion, renovable: true, token);

    private async Task<HttpResponseMessage> EnviarAsync(
        Func<string?, HttpRequestMessage> crearPeticion, bool renovable, CancellationToken token)
    {
        var respuesta = await _http.SendAsync(crearPeticion(_sesion.AccessToken), token);
        if (!renovable
            || respuesta.StatusCode != HttpStatusCode.Unauthorized
            || string.IsNullOrEmpty(_sesion.RefreshToken))
            return respuesta;
        if (!await RenovarSesionAsync(token))
        {
            _registro.LogWarning("La renovación de sesión no prosperó; se informa el 401 original.");
            return respuesta;
        }
        respuesta.Dispose();
        return await _http.SendAsync(crearPeticion(_sesion.AccessToken), token);
    }

    private async Task<bool> RenovarSesionAsync(CancellationToken token)
    {
        var refresh = _sesion.RefreshToken;
        if (string.IsNullOrEmpty(refresh))
            return false;
        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, new Uri(ResolverBase() + "identidad/sesion/renovar"));
        peticion.Headers.TryAddWithoutValidation("Cookie", $"{CookieRefresh}={refresh}");
        using var respuesta = await _http.SendAsync(peticion, token);
        if (respuesta.StatusCode != HttpStatusCode.OK)
            return false;
        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionApi>(Json, token);
        var nuevoRefresh = LeerCookieRefresh(respuesta);
        if (sesion is null || string.IsNullOrEmpty(sesion.AccessToken) || nuevoRefresh is null)
            return false;
        await _sesion.RenovarTokensAsync(sesion.AccessToken, nuevoRefresh, token);
        return true;
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken token)
    {
        if (respuesta.IsSuccessStatusCode)
            return;
        string? titulo = null;
        string? correlacion = null;
        Dictionary<string, IReadOnlyList<string>>? errores = null;
        try
        {
            using var documento = JsonDocument.Parse(
                await respuesta.Content.ReadAsStringAsync(token));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("title", out var tituloJson))
                    titulo = tituloJson.GetString();
                if (raiz.TryGetProperty("correlationId", out var correlacionJson))
                    correlacion = correlacionJson.GetString();
                if (raiz.TryGetProperty("errors", out var erroresJson)
                    && erroresJson.ValueKind == JsonValueKind.Object)
                    errores = erroresJson.EnumerateObject().ToDictionary(
                        propiedad => propiedad.Name,
                        propiedad => (IReadOnlyList<string>)propiedad.Value
                            .EnumerateArray()
                            .Select(valor => valor.GetString() ?? string.Empty)
                            .ToArray());
            }
        }
        catch (JsonException)
        {
            // Cuerpos sin JSON (por ejemplo 401 o 403 vacíos): el estado basta.
        }
        throw new ErrorApiException(
            (int)respuesta.StatusCode, titulo, correlacion, errores);
    }

    private HttpRequestMessage PeticionJson(
        HttpMethod metodo, string ruta, string? accessToken, object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, new Uri(ResolverBase() + ruta));
        if (accessToken is not null)
            peticion.Headers.Authorization = new("Bearer", accessToken);
        if (cuerpo is not null)
            peticion.Content = new StringContent(
                JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json");
        return peticion;
    }

    private HttpRequestMessage PeticionMultipart(
        byte[] bytes, string nombreArchivo, string? accessToken)
    {
        var peticion = new HttpRequestMessage(
            HttpMethod.Post, new Uri(ResolverBase() + "precios-alimentos/importar"));
        if (accessToken is not null)
            peticion.Headers.Authorization = new("Bearer", accessToken);
        var parte = new StreamContent(new MemoryStream(bytes));
        parte.Headers.ContentType = new("application/pdf");
        var cuerpo = new MultipartFormDataContent { { parte, "archivo", nombreArchivo } };
        peticion.Content = cuerpo;
        return peticion;
    }

    // La API vive bajo /api del mismo origen lógico en despliegue; si la
    // configuración trae una URL absoluta (desarrollo, pruebas) se usa tal cual.
    private string ResolverBase()
    {
        var base_ = _baseUrl ?? "/api";
        if (base_.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return base_.EndsWith('/') ? base_ : $"{base_}/";
        var contexto = _contexto.HttpContext
            ?? throw new InvalidOperationException(
                "La URL de la API es relativa y no hay petición en curso para resolverla.");
        var host = $"{contexto.Request.Scheme}://{contexto.Request.Host}";
        if (!base_.StartsWith('/'))
            return $"{host}/{base_}/";
        return base_.EndsWith('/') ? $"{host}{base_}" : $"{host}{base_}/";
    }

    private static string? LeerCookieRefresh(HttpResponseMessage respuesta)
    {
        if (!respuesta.Headers.TryGetValues("Set-Cookie", out var valores))
            return null;
        var cookie = valores.FirstOrDefault(valor =>
            valor.StartsWith($"{CookieRefresh}=", StringComparison.Ordinal));
        if (cookie is null)
            return null;
        var cuerpo = cookie[$"{CookieRefresh}=".Length..];
        var fin = cuerpo.IndexOf(';');
        return (fin < 0 ? cuerpo : cuerpo[..fin]).Trim();
    }
}
