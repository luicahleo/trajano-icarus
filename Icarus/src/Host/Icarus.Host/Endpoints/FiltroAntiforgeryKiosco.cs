using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Microsoft.Extensions.Options;

namespace Icarus.Host.Endpoints;

// Defensa CSRF de las mutaciones del kiosco: exige el encabezado propio y, si
// llega un Origin, que pertenezca a la allowlist. Un origen hermano se rechaza.
public sealed class FiltroAntiforgeryKiosco(IOptions<OpcionesKiosco> opciones) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var origen = http.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origen) &&
            !opciones.Value.OrigenesPermitidos.Contains(origen, StringComparer.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!http.Request.Headers.ContainsKey(OpcionesKiosco.EncabezadoAntiforgery))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return await next(context);
    }
}
