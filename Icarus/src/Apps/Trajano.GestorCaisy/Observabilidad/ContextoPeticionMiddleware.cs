using System.Diagnostics;
using Serilog.Context;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Correlación de la petición MVC: conserva un UUID entrante válido o
/// genera uno propio y publica el TraceId W3C. Contexto separado del resumen
/// HTTP para que este último no dependa de scopes internos.</summary>
public sealed class ContextoPeticionMiddleware
{
    public const string CorrelationHeader = "X-Correlation-ID";
    public const string TraceHeader = "X-Trace-Id";

    private readonly RequestDelegate _siguiente;

    public ContextoPeticionMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        var entrante = contexto.Request.Headers[CorrelationHeader].FirstOrDefault();
        var correlationId = Guid.TryParse(entrante, out var recibido)
            ? recibido.ToString()
            : Guid.NewGuid().ToString();
        contexto.Items[CorrelationHeader] = correlationId;
        contexto.Response.Headers[CorrelationHeader] = correlationId;

        var traceId = Activity.Current?.TraceId.ToString()
            ?? ActivityTraceId.CreateRandom().ToString();
        contexto.TraceIdentifier = traceId;
        contexto.Items[TraceHeader] = traceId;
        contexto.Response.Headers[TraceHeader] = traceId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _siguiente(contexto);
        }
    }
}
