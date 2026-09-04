using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Filtros;

// Traduce los errores del cliente de la API a respuestas de la aplicación:
// sesión vencida cierra y manda a acceder, recurso ausente responde 404 y el
// resto cae a la página genérica de error. Los mensajes son técnicos y
// genéricos; nunca transportan contenido documental (anti-PII).
public sealed class FiltroErroresApi : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ErrorApiException error)
            return;
        context.ExceptionHandled = true;
        if (error.Estado == StatusCodes.Status401Unauthorized)
        {
            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = new RedirectResult("/Sesion/Acceder");
            return;
        }
        context.Result = error.Estado == StatusCodes.Status404NotFound
            ? new NotFoundResult()
            : new RedirectResult($"/Sesion/Error?codigo={error.Estado}");
    }
}
