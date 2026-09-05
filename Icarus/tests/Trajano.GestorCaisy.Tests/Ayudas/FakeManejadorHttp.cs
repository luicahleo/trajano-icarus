using System.Net;
using System.Text;

namespace Trajano.GestorCaisy.Tests.Ayudas;

public sealed record PeticionCapturada(
    HttpMethod Metodo, Uri Uri, string? Cuerpo,
    string? Autorizacion, string? Cookie, string? TipoDeContenido);

// Manejador HTTP de prueba: graba cada petición y devuelve las respuestas
// encoladas en orden. Si se agotan, falla de forma explícita para que el test
// indique exactamente qué flujo no se programó.
public sealed class FakeManejadorHttp : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _respuestas = new();

    public List<PeticionCapturada> Peticiones { get; } = [];

    public void Responder(HttpStatusCode estado, string? cuerpoJson = null,
        IEnumerable<KeyValuePair<string, string>>? cabeceras = null,
        byte[]? contenido = null, string? tipoDeContenido = null)
    {
        var respuesta = new HttpResponseMessage(estado);
        if (contenido is not null)
        {
            respuesta.Content = new ByteArrayContent(contenido);
            if (tipoDeContenido is not null)
                respuesta.Content.Headers.ContentType = new(tipoDeContenido);
        }
        else if (cuerpoJson is not null)
        {
            respuesta.Content = new StringContent(cuerpoJson, Encoding.UTF8, "application/json");
        }
        if (cabeceras is not null)
        {
            foreach (var (nombre, valor) in cabeceras)
                respuesta.Headers.TryAddWithoutValidation(nombre, valor);
        }
        _respuestas.Enqueue(respuesta);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var tipoDeContenido = request.Content?.Headers.ContentType?.MediaType;
        Peticiones.Add(new PeticionCapturada(
            request.Method, request.RequestUri!, cuerpo,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("Cookie", out var cookies)
                ? string.Join("; ", cookies)
                : null,
            tipoDeContenido));

        if (_respuestas.Count == 0)
            throw new InvalidOperationException(
                $"No hay respuesta programada para {request.Method} {request.RequestUri}");
        return _respuestas.Dequeue();
    }
}
