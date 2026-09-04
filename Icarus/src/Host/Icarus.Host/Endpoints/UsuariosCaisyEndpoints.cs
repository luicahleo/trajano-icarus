using Icarus.Identity.Application.UsuariosCaisy;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;

namespace Icarus.Host.Endpoints;

// Administración de cuentas CAISY (spec SP8): solo el Administrador de
// plataforma crea, desactiva y asigna funcionalidades. Nunca se crean cuentas
// CAISY desde la aplicación de oficina.
public static class UsuariosCaisyEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosCaisy(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/usuarios-caisy")
            .RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        grupo.MapPost("/", async (CrearUsuarioCaisyRequest cuerpo, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearUsuarioCaisyCommand(
                cuerpo.Email, cuerpo.Contrasena, cuerpo.Funcionalidades));
            return Results.Created($"/usuarios-caisy/{id}", new { id });
        });

        grupo.MapGet("/", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarUsuariosCaisyQuery())));

        grupo.MapPut("/{id:guid}/funcionalidades", async (
            Guid id, DefinirFuncionalidadesCaisyRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new DefinirFuncionalidadesCaisyCommand(id, cuerpo.Funcionalidades));
            return Results.NoContent();
        });

        // Desactivar: soft delete del glosario; la cuenta deja de autenticar y
        // de renovar sesión.
        grupo.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarUsuarioCaisyCommand(id));
            return Results.NoContent();
        });

        return app;
    }

    private sealed record CrearUsuarioCaisyRequest(string Email, string Contrasena, IReadOnlyList<string> Funcionalidades);
    private sealed record DefinirFuncionalidadesCaisyRequest(IReadOnlyList<string> Funcionalidades);
}
