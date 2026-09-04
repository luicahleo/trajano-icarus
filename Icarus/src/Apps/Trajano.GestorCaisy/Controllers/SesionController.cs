using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Inicio y cierre de sesión. Las credenciales viajan solo al momento del POST
// hacia la API; los tokens quedan en la cookie protegida y el correo se usa
// únicamente para identificar la sesión en la barra (anti-PII: jamás en logs).
public sealed class SesionController(IApiIcarusClient api) : Controller
{
    [HttpGet]
    public IActionResult Acceder()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Precios");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acceder(FormularioAcceso formulario)
    {
        if (!ModelState.IsValid)
            return View(formulario);
        SesionApi sesion;
        try
        {
            sesion = await api.IniciarSesionAsync(formulario.Correo, formulario.Contrasena);
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 401)
        {
            // Anti-enumeración: un solo mensaje genérico para cualquier fallo.
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(formulario);
        }
        var principal = PrincipalGestorcaisy.Crear(
            sesion.AccessToken, sesion.RefreshToken
                ?? throw new ErrorApiException(502, "La sesión no trajo renovación"));
        if (principal is null)
        {
            ModelState.AddModelError(
                string.Empty, "No se pudo iniciar la sesión; inténtelo de nuevo.");
            return View(formulario);
        }
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            PrincipalGestorcaisy.ConCorreo(principal, formulario.Correo));
        return RedirectToAction("Index", "Precios");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denegado() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Acceder));
    }

    // Destino del reejecute de errores (UseExceptionHandler y páginas de
    // estado): mensaje genérico según el código, sin detalles internos.
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Error(int? codigo)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        Response.StatusCode = codigo is >= 400 and <= 599 ? codigo.Value : 500;
        ViewData["Codigo"] = codigo;
        return View();
    }
}
