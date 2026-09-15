using System.Diagnostics;
using Icarus.BuildingBlocks.Observability;
using Serilog.Context;

namespace Icarus.Host.Observability;

/// <summary>Establece el TraceId de la ejecución y el SessionId de la pestaña
/// como contexto del evento, separado del resumen HTTP. No resuelve la ruta:
/// eso ocurre después de routing y lo aporta el resumen seguro.</summary>
public sealed class ContextoTrazaMiddleware
{
    private readonly RequestDelegate _siguiente;

    public ContextoTrazaMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        var traceId = Activity.Current?.TraceId.ToString()
            ?? DiagnosticContext.ObtenerTraceId(contexto)
            ?? ActivityTraceId.CreateRandom().ToString();

        contexto.TraceIdentifier = traceId;
        DiagnosticContext.EstablecerTraceId(contexto, traceId);
        contexto.Response.Headers[DiagnosticIds.TraceHeader] = traceId;

        using (LogContext.PushProperty("TraceId", traceId))
        {
            var sessionId = contexto.Request.Headers[DiagnosticIds.SessionHeader].FirstOrDefault();
            if (DiagnosticIds.EsSessionId(sessionId))
            {
                using (LogContext.PushProperty("SessionId", sessionId))
                {
                    await _siguiente(contexto);
                }
            }
            else
            {
                await _siguiente(contexto);
            }
        }
    }
}
