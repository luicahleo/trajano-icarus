using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class ClientesEndpoints
{
    public static IEndpointRouteBuilder MapClientes(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/clientes");

        // Gestión de clientes: solo Administrador (spec).
        grupo.MapPost("/", async (CrearClienteCommand command, ISender mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/clientes/{id}", new { id });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        grupo.MapGet("/", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarClientesQuery())))
            .RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        grupo.MapPost("/{id:guid}/suspender", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new SuspenderClienteCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        grupo.MapPost("/{id:guid}/reactivar", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new ReactivarClienteCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        grupo.MapPut("/{id:guid}/modulos", async (Guid id, DefinirModulosRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new DefinirModulosClienteCommand(id, cuerpo.Modulos));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);

        // Gestión de trabajadores: Administrador, y Cliente sobre su propia
        // empresa (spec; el filtro de tenant acota la segunda parte).
        grupo.MapPost("/{clienteId:guid}/trabajadores",
            async (Guid clienteId, CrearTrabajadorRequest cuerpo, ISender mediator) =>
            {
                var id = await mediator.Send(new CrearTrabajadorCommand(
                    clienteId, cuerpo.Nombre, cuerpo.DocumentoIdentidad, cuerpo.Cargo,
                    cuerpo.FechaIngreso, cuerpo.Email, cuerpo.Contrasena));
                return Results.Created($"/clientes/{clienteId}/trabajadores/{id}", new { id });
            }).RequireAuthorization(PoliticasAutorizacion.GestionTrabajadores);

        grupo.MapGet("/{clienteId:guid}/trabajadores", async (Guid clienteId, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarTrabajadoresQuery(clienteId))))
            .RequireAuthorization(PoliticasAutorizacion.GestionTrabajadores);

        grupo.MapPost("/trabajadores/{id:guid}/cese", async (Guid id, CeseTrabajadorRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new CesarTrabajadorCommand(id, cuerpo.FechaCese));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.GestionTrabajadores);

        grupo.MapDelete("/trabajadores/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarTrabajadorCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.GestionTrabajadores);

        return app;
    }

    // Sondeo de entitlement: el mecanismo se construye y se prueba en este
    // incremento aunque aún no haya endpoints de módulos de negocio (spec).
    // Lo mapea Program solo en Development y Testing.
    public static IEndpointRouteBuilder MapSondeoEntitlement(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/clientes/sondeo");

        grupo.MapGet("/gestion-avicola", () => Results.Ok(new { estado = "ok" }))
            .RequireAuthorization(PoliticasClientes.RequiereGestionAvicola);
        grupo.MapGet("/control-acceso", () => Results.Ok(new { estado = "ok" }))
            .RequireAuthorization(PoliticasClientes.RequiereControlAcceso);

        return app;
    }

    private sealed record DefinirModulosRequest(IReadOnlyList<string> Modulos);

    private sealed record CrearTrabajadorRequest(
        string Nombre, string DocumentoIdentidad, string Cargo, DateOnly FechaIngreso,
        string Email, string Contrasena);

    private sealed record CeseTrabajadorRequest(DateOnly FechaCese);
}
