using System.Globalization;
using System.Security.Claims;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.Host.Autorizacion;
using Icarus.Host.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icarus.Host.Endpoints;

public static class KioscoSesionEndpoints
{
    private const string RutaCookie = "/api/control-acceso";

    public static IEndpointRouteBuilder MapKioscoSesion(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso/kiosco");

        // Activación: verifica credenciales de Cliente y emite SOLO la cookie
        // restringida. No devuelve access token ni refresh administrativo.
        grupo.MapPost("/sesion", async Task<IResult> (
            [FromBody] CredencialesKiosco credenciales,
            ActivacionKioscoServicio servicio,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var resultado = await servicio.ActivarAsync(
                credenciales.Email, credenciales.Contrasena, cancellationToken);
            if (resultado is null)
                return Results.Unauthorized();

            EstablecerCookie(http, resultado.Token, resultado.ExpiraEnUtc);
            return Results.NoContent();
        })
            .AddEndpointFilter<FiltroAntiforgeryKiosco>()
            .RequireRateLimiting("kiosco-activacion");

        // Recuperación tras reiniciar el navegador/Android: el handler de
        // autenticación ya comprobó vigencia, tenant y módulo online.
        grupo.MapGet("/sesion", (HttpContext http) =>
        {
            var expira = http.User.FindFirstValue(ClaimsKiosco.ExpiraUtc);
            return DateTimeOffset.TryParse(
                expira, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiraEnUtc)
                ? Results.Ok(new { expiraEnUtc })
                : Results.Unauthorized();
        })
            .RequireAuthorization(PoliticasKiosco.Autenticado);

        // Salida del modo kiosco: exige reautenticación del mismo cliente.
        grupo.MapDelete("/sesion", async Task<IResult> (
            [FromBody] CredencialesKiosco credenciales,
            ActivacionKioscoServicio servicio,
            HttpContext http,
            ICurrentUser actual,
            CancellationToken cancellationToken) =>
        {
            if (actual.UsuarioId is not { } sesionId)
                return Results.Unauthorized();

            var revocada = await servicio.CerrarAsync(
                sesionId, credenciales.Email, credenciales.Contrasena, cancellationToken);
            if (!revocada)
                return Results.Unauthorized();

            http.Response.Cookies.Delete(OpcionesKiosco.Cookie, new CookieOptions { Path = RutaCookie });
            return Results.NoContent();
        })
            .RequireAuthorization(PoliticasKiosco.Autenticado)
            .AddEndpointFilter<FiltroAntiforgeryKiosco>();

        return app;
    }

    private static void EstablecerCookie(HttpContext http, string token, DateTimeOffset expiraEnUtc)
    {
        // Cookie host-only (sin Domain) y HttpOnly: nunca accesible por script.
        http.Response.Cookies.Append(OpcionesKiosco.Cookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = RutaCookie,
            Expires = expiraEnUtc,
        });
    }

    public sealed record CredencialesKiosco(string Email, string Contrasena);
}
