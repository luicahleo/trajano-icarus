using Icarus.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Routing;
using Serilog.AspNetCore;
using Serilog.Events;

namespace Icarus.Host.Observability;

/// <summary>Adapta el resumen HTTP de Serilog al contrato propio: un solo evento
/// por petición, patrón de ruta (nunca el pathname), sin query, cuerpos ni
/// excepción cruda, y contexto de identidad recogido tras autenticar.</summary>
public static class RegistroHttpSeguro
{
    public const string NombreEvento = "http.request.completed";
    public const string RutaSinResolver = "unmatched";

    public static void Configurar(RequestLoggingOptions opciones)
    {
        opciones.MessageTemplate =
            "{EventName}: {Method} {RoutePattern} respondió {StatusCode} en {DurationMs} ms";
        opciones.GetLevel = (contexto, _, excepcion) =>
            excepcion is not null || contexto.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : LogEventLevel.Information;
        opciones.GetMessageTemplateProperties = (contexto, _, duracionMs, estado) =>
        {
            var patron = PatronRuta(contexto);
            var propiedades = new List<LogEventProperty>
            {
                new("EventName", new ScalarValue(NombreEvento)),
                new("Method", new ScalarValue(contexto.Request.Method)),
                new("RoutePattern", new ScalarValue(patron)),
                // Sombra el RequestPath concreto que hereda el scope del framework.
                new("RequestPath", new ScalarValue(patron)),
                new("StatusCode", new ScalarValue(estado)),
                new("DurationMs", new ScalarValue(duracionMs)),
            };

            if (DiagnosticContext.ObtenerErrorId(contexto) is { } errorId)
                propiedades.Add(new LogEventProperty("ErrorId", new ScalarValue(errorId)));

            return propiedades;
        };
        opciones.EnrichDiagnosticContext = (diagnostico, contexto) =>
        {
            if (contexto.Items.TryGetValue(CorrelationIdMiddleware.Header, out var correlacion)
                && correlacion is string correlationId)
                diagnostico.Set("CorrelationId", correlationId);

            if (DiagnosticContext.ObtenerTraceId(contexto) is { } traceId)
                diagnostico.Set("TraceId", traceId);

            var sessionId = contexto.Request.Headers[DiagnosticIds.SessionHeader].FirstOrDefault();
            if (DiagnosticIds.EsSessionId(sessionId))
                diagnostico.Set("SessionId", sessionId!);

            if (DiagnosticContext.ObtenerClienteId(contexto) is { } clienteId)
                diagnostico.Set("ClienteId", clienteId);

            if (DiagnosticContext.ObtenerRol(contexto) is { } rol)
                diagnostico.Set("Rol", rol);
        };
    }

    /// <summary>Patrón de ruta resuelto, o un valor cerrado cuando no hay
    /// endpoint. Nunca copia el pathname recibido. Prefiere el patrón capturado
    /// por ContextoRutaMiddleware, que sobrevive al desenrollado de scopes.</summary>
    public static string PatronRuta(HttpContext contexto)
    {
        if (contexto.Items.TryGetValue(ContextoRutaMiddleware.Item, out var capturado)
            && capturado is string patronCapturado && patronCapturado.Length > 0)
            return patronCapturado;

        if (contexto.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } patron })
            return patron;

        return RutaSinResolver;
    }
}
