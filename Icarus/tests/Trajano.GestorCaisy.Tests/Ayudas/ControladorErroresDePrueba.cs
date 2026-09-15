using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Trajano.GestorCaisy.Tests.Ayudas;

/// <summary>Controlador solo de pruebas que lanza excepciones con canario y
/// excepción interna para verificar que ningún evento serializado las filtra.</summary>
[Route("__pruebas/errores")]
public sealed class ControladorErroresDePrueba : Controller
{
    public const string Canario = "CANARIO_EXCEPCION";
    public const string CanarioInterna = "CANARIO_INTERNA";
    public const string ClaveFallarPaginaError = "Trajano.Pruebas.FallarPaginaError";

    [HttpGet("lanzar")]
    public IActionResult Lanzar() =>
        throw new InvalidOperationException(Canario, new Exception(CanarioInterna));

    [HttpGet("pagina-error")]
    public IActionResult PaginaError()
    {
        HttpContext.Items[ClaveFallarPaginaError] = true;
        throw new InvalidOperationException(Canario, new Exception(CanarioInterna));
    }

    [HttpGet("respuesta-iniciada")]
    public async Task RespuestaIniciada()
    {
        await Response.WriteAsync("respuesta parcial");
        await Response.Body.FlushAsync();
        throw new InvalidOperationException(Canario, new Exception(CanarioInterna));
    }

    [HttpGet("cancelacion")]
    public IActionResult Cancelacion()
    {
        HttpContext.Features.Set<IHttpRequestLifetimeFeature>(new PeticionYaCancelada());
        throw new OperationCanceledException(Canario, new Exception(CanarioInterna));
    }

    private sealed class PeticionYaCancelada : IHttpRequestLifetimeFeature
    {
        public PeticionYaCancelada() =>
            RequestAborted = new CancellationToken(canceled: true);

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => RequestAborted = new CancellationToken(canceled: true);
    }
}

/// <summary>Filtro solo de pruebas: cuando la petición marcó que la página de
/// error debe fallar, lanza al ejecutarse la acción Error de Sesion.</summary>
public sealed class FiltroFalloPaginaDeErrorPrueba : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items.ContainsKey(ControladorErroresDePrueba.ClaveFallarPaginaError)
            && context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controlador)
            && string.Equals(controlador, "Sesion", StringComparison.OrdinalIgnoreCase)
            && context.ActionDescriptor.RouteValues.TryGetValue("action", out var accion)
            && string.Equals(accion, "Error", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                ControladorErroresDePrueba.Canario,
                new Exception(ControladorErroresDePrueba.CanarioInterna));
        }

        return next();
    }
}
