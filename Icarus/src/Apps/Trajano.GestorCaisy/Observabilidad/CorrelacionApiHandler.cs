using System.Diagnostics;
using System.Net;
using Serilog.Context;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Correlaciona cada salto HTTP de GestorCaisy hacia la API: genera un
/// UUID propio por envío real (incluidos refresh y reintento), lo envía en
/// X-Correlation-ID y lo registra como DownstreamCorrelationId sin tocar el
/// CorrelationId entrante ni serializar contenido, URL completa ni tokens.</summary>
public sealed class CorrelacionApiHandler : DelegatingHandler
{
    public const string Header = "X-Correlation-ID";
    public const string EventoEnvio = "http.client.send";

    private readonly ILogger<CorrelacionApiHandler> _registro;

    public CorrelacionApiHandler(ILogger<CorrelacionApiHandler> registro) => _registro = registro;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var idSaliente = Guid.NewGuid().ToString();
        request.Headers.Remove(Header);
        request.Headers.TryAddWithoutValidation(Header, idSaliente);
        var inicio = Stopwatch.GetTimestamp();

        using (LogContext.PushProperty("DownstreamCorrelationId", idSaliente))
        {
            try
            {
                var respuesta = await base.SendAsync(request, cancellationToken);
                RegistrarEnvio(request, respuesta.StatusCode, inicio);
                return respuesta;
            }
            catch (Exception ex)
            {
                RegistrarFallo(request, ex, inicio);
                throw;
            }
        }
    }

    private void RegistrarEnvio(HttpRequestMessage peticion, HttpStatusCode estado, long inicio) =>
        _registro.LogInformation(
            "{EventName}: {Method} {RoutePattern} respondió {StatusCode} en {DurationMs} ms",
            EventoEnvio, peticion.Method.Method, Ruta(peticion), (int)estado,
            Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

    // El log vive fuera del catch a propósito: solo se registra el tipo de
    // excepción, nunca la excepción cruda con su stack ni el cuerpo.
    private void RegistrarFallo(HttpRequestMessage peticion, Exception ex, long inicio) =>
        _registro.LogWarning(
            "{EventName}: {Method} {RoutePattern} falló con {ExceptionType} en {DurationMs} ms",
            EventoEnvio, peticion.Method.Method, Ruta(peticion), ex.GetType().FullName,
            Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

    // La plantilla llega por Options desde la operación que conoce la ruta; sin
    // metadato se registra "unmatched". Nunca se inspecciona AbsolutePath.
    private static string Ruta(HttpRequestMessage peticion) =>
        peticion.RutaSegura();
}
