using System.Security.Claims;
using System.Text.Encodings.Web;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Application.Tiempo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icarus.ControlAcceso.Infrastructure.Kiosco;

// Esquema de cookie host-only exclusivo del kiosco. Nunca emite ni acepta JWT
// administrativo: resuelve la sesión por hash y compone un principal con rol
// "Kiosco" que no satisface las políticas de administración.
public sealed class AutenticacionKioscoHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IServiceScopeFactory _alcanceFactory;

    public AutenticacionKioscoHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory logger,
        UrlEncoder codificador,
        IServiceScopeFactory alcanceFactory)
        : base(opciones, logger, codificador)
    {
        _alcanceFactory = alcanceFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(OpcionesKiosco.Cookie, out var token) ||
            string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        string hash;
        try
        {
            hash = CredencialKiosco.CalcularHash(token);
        }
        catch (ArgumentException)
        {
            return AuthenticateResult.Fail("Credencial de kiosco inválida.");
        }

        // Scope propio: la consulta de autenticación no contamina el
        // DbContext de la petición, que aún no tiene usuario autenticado.
        using var alcance = _alcanceFactory.CreateScope();
        var sesiones = alcance.ServiceProvider.GetRequiredService<IRepositorioSesionesKiosco>();
        var elegibilidad = alcance.ServiceProvider.GetRequiredService<IConsultaElegibilidadAcceso>();
        var reloj = alcance.ServiceProvider.GetRequiredService<IRelojAcceso>();

        var sesion = await sesiones.ObtenerPorHashAsync(hash, Context.RequestAborted);
        if (sesion is null || !sesion.EstaVigente(reloj.ObtenerInstanteUtc()))
            return AuthenticateResult.Fail("Sesión de kiosco inválida.");

        var estado = await elegibilidad.EvaluarAsync(sesion.ClienteId, null, Context.RequestAborted);
        if (!estado.ClienteActivo || !estado.ModuloHabilitado)
            return AuthenticateResult.Fail("Sesión de kiosco inválida.");

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimsKiosco.Subject, sesion.Id.ToString()),
            new Claim(ClaimsKiosco.Rol, ClaimsKiosco.RolValor),
            new Claim(ClaimsKiosco.ClienteId, sesion.ClienteId.ToString()),
            new Claim(ClaimsKiosco.ExpiraUtc, sesion.ExpiraEnUtc.ToString("O")),
        ], Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
