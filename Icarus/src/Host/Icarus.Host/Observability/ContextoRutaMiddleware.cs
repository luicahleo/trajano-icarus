using Microsoft.AspNetCore.Routing;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Icarus.Host.Observability;

/// <summary>Fija el patrón de ruta seguro después de routing y lo publica como
/// contexto de la ejecución: sombrea el RequestPath concreto que hereda el
/// scope del framework y lo conserva en Items para el resumen y el log de
/// error exterior, que corren con los scopes ya desenrollados.</summary>
public sealed class ContextoRutaMiddleware
{
    public const string Item = "Icarus.Observabilidad.RoutePattern";

    private readonly RequestDelegate _siguiente;

    public ContextoRutaMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        var patron = (contexto.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (string.IsNullOrEmpty(patron))
            patron = RegistroHttpSeguro.RutaSinResolver;

        contexto.Items[Item] = patron;
        using (LogContext.Push(new EnriquecedorRutaSegura(patron)))
        {
            await _siguiente(contexto);
        }
    }

    // LogContext.PushProperty no reemplaza una propiedad ya presente (el scope
    // del framework añade RequestPath con AddPropertyIfAbsent); este enricher
    // la sobrescribe dentro del alcance de la ruta.
    private sealed class EnriquecedorRutaSegura(string patron) : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // RoutePattern solo si el evento no trae uno propio (una llamada
            // saliente declara su propia plantilla); RequestPath siempre se
            // sombrea con el patrón seguro.
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RoutePattern", patron));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RequestPath", patron));
        }
    }
}
