using System.Diagnostics;
using System.Security.Claims;
using Icarus.BuildingBlocks.Observability;
using Icarus.Identity.Domain;
using Microsoft.AspNetCore.Routing;

namespace Icarus.Host.Observability;

public sealed partial class RequestObservabilityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
        context.TraceIdentifier = traceId;
        DiagnosticContext.EstablecerTraceId(context, traceId);
        context.Response.Headers[DiagnosticIds.TraceHeader] = traceId;

        var scope = CrearScope(context, traceId);
        var inicio = Stopwatch.GetTimestamp();
        using (logger.BeginScope(scope))
        {
            await next(context);

            var patronRuta = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["ErrorId"] = DiagnosticContext.ObtenerErrorId(context),
            }))
            {
                LogRequestCompleted(
                    logger,
                    context.Request.Method,
                    patronRuta,
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            }
        }
    }

    private static Dictionary<string, object?> CrearScope(HttpContext context, string traceId)
    {
        var scope = new Dictionary<string, object?>
        {
            ["CorrelationId"] = context.Items[CorrelationIdMiddleware.Header],
            ["TraceId"] = traceId,
        };

        var sessionId = context.Request.Headers[DiagnosticIds.SessionHeader].FirstOrDefault();
        if (DiagnosticIds.EsSessionId(sessionId)) scope["SessionId"] = sessionId;

        var clienteId = context.User.FindFirstValue(ClaimsIdentidad.ClienteId);
        if (Guid.TryParse(clienteId, out var cliente)) scope["ClienteId"] = cliente;

        var rol = context.User.FindFirstValue(ClaimsIdentidad.Rol);
        if (!string.IsNullOrWhiteSpace(rol)) scope["Rol"] = rol;

        return scope;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{EventName}: {Method} {RoutePattern} respondió {StatusCode} en {DurationMs} ms")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string routePattern,
        int statusCode,
        double durationMs,
        string eventName = "http.request.completed");
}
