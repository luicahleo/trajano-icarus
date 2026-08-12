using Icarus.BuildingBlocks.Application;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Application.Usuarios;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.Extensions.Options;

namespace Icarus.Host.Endpoints;

public static class IdentidadEndpoints
{
    public const string CookieRefresh = "icarus_refresh";

    public static IEndpointRouteBuilder MapIdentidad(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/identidad");

        grupo.MapPost("/sesion", async (
            IniciarSesionCommand command, ISender mediator, HttpContext http, IOptions<OpcionesJwt> jwt) =>
        {
            var sesion = await mediator.Send(command);
            EstablecerCookieRefresh(http, sesion.RefreshToken, jwt.Value.DiasRefreshToken);
            return Results.Ok(new { sesion.AccessToken, sesion.ExpiraEnSegundos });
        });

        grupo.MapPost("/sesion/renovar", async Task<IResult> (
            HttpContext http, ISender mediator, IOptions<OpcionesJwt> jwt) =>
        {
            var refresh = http.Request.Cookies[CookieRefresh];
            if (string.IsNullOrEmpty(refresh))
                return Results.Unauthorized();

            var sesion = await mediator.Send(new RenovarSesionCommand(refresh));
            EstablecerCookieRefresh(http, sesion.RefreshToken, jwt.Value.DiasRefreshToken);
            return Results.Ok(new { sesion.AccessToken, sesion.ExpiraEnSegundos });
        });

        grupo.MapPost("/usuarios", async (CrearUsuarioCommand command, ISender mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/identidad/usuarios/{id}", new { id });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        // Sesión actual: el frontend la usa para las guardas y navegación por rol.
        grupo.MapGet("/me", (ICurrentUser actual) =>
            Results.Ok(new { actual.UsuarioId, actual.Rol, actual.ClienteId }))
            .RequireAuthorization();

        return app;
    }

    private static void EstablecerCookieRefresh(HttpContext http, string refreshToken, int diasValidez)
    {
        // El refresh token viaja solo en cookie HttpOnly; el cuerpo de la
        // respuesta lleva únicamente el access token (spec).
        http.Response.Cookies.Append(CookieRefresh, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(diasValidez),
        });
    }
}
