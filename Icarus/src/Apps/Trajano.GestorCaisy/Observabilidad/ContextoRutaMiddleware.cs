using Microsoft.AspNetCore.Routing;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Captura el patrón de ruta del endpoint original una sola vez, antes
/// de las reejecuciones de error, y lo publica como contexto de la ejecución:
/// sombrea el RequestPath concreto en todos los eventos internos. Nunca copia
/// el pathname recibido.</summary>
public sealed class ContextoRutaMiddleware
{
    public const string Item = "Trajano.Observabilidad.RoutePattern";

    private readonly RequestDelegate _siguiente;

    public ContextoRutaMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        if (!contexto.Items.TryGetValue(Item, out var capturado) || capturado is not string)
        {
            capturado = (contexto.GetEndpoint() as RouteEndpoint)
                ?.RoutePattern.RawText ?? RegistroHttpSeguro.RutaSinResolver;
            contexto.Items[Item] = capturado;
        }

        var patron = (string)capturado;
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
