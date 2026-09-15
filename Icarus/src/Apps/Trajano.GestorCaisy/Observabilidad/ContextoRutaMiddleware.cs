using Microsoft.AspNetCore.Routing;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Captura el patrón de ruta del endpoint original una sola vez, antes
/// de las reejecuciones de error, para que el resumen conserve la ruta real.
/// Nunca copia el pathname recibido.</summary>
public sealed class ContextoRutaMiddleware
{
    public const string Item = "Trajano.Observabilidad.RoutePattern";

    private readonly RequestDelegate _siguiente;

    public ContextoRutaMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        if (!contexto.Items.ContainsKey(Item))
        {
            contexto.Items[Item] = (contexto.GetEndpoint() as RouteEndpoint)
                ?.RoutePattern.RawText ?? RegistroHttpSeguro.RutaSinResolver;
        }

        await _siguiente(contexto);
    }
}
