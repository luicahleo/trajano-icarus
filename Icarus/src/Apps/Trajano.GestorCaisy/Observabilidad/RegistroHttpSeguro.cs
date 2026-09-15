using System.Security.Claims;
using Serilog.AspNetCore;
using Serilog.Events;
using Trajano.GestorCaisy.Autenticacion;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Resumen HTTP propio de GestorCaisy: un evento por petición externa,
/// patrón de ruta (nunca el pathname), sin query, cuerpos ni excepción cruda.</summary>
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

            if (contexto.Items.TryGetValue(ExcepcionesSegurasMiddleware.ErrorIdItem, out var errorId)
                && errorId is string errorIdTexto)
                propiedades.Add(new LogEventProperty("ErrorId", new ScalarValue(errorIdTexto)));

            return propiedades;
        };
        opciones.EnrichDiagnosticContext = (diagnostico, contexto) =>
        {
            if (contexto.Items.TryGetValue(ContextoPeticionMiddleware.CorrelationHeader, out var correlacion)
                && correlacion is string correlationId)
                diagnostico.Set("CorrelationId", correlationId);

            if (contexto.Items.TryGetValue(ContextoPeticionMiddleware.TraceHeader, out var trace)
                && trace is string traceId)
                diagnostico.Set("TraceId", traceId);

            var rol = contexto.User.FindFirstValue(ConstantesAutorizacion.ClaimRol);
            if (!string.IsNullOrWhiteSpace(rol))
                diagnostico.Set("Rol", rol);
        };
    }

    public static string PatronRuta(HttpContext contexto)
    {
        if (contexto.Items.TryGetValue(ContextoRutaMiddleware.Item, out var patron)
            && patron is string patronTexto)
            return patronTexto;

        return (contexto.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)
            ?.RoutePattern.RawText ?? RutaSinResolver;
    }
}
