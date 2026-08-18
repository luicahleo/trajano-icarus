using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class GestionAvicolaEndpoints
{
    public static IEndpointRouteBuilder MapGestionAvicola(this IEndpointRouteBuilder app)
    {
        var politicaGranjas = PoliticasClientes.Para(Funcionalidades.Granjas);
        var politicaGalpones = PoliticasClientes.Para(Funcionalidades.Galpones);
        var granjas = app.MapGroup("/granjas");
        granjas.MapPost("/", async (CrearGranjaRequest cuerpo, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearGranjaCommand(cuerpo.Nombre));
            return Results.Created($"/granjas/{id}", new { id });
        }).RequireAuthorization(politicaGranjas);
        granjas.MapGet("/", async (ISender mediator) => Results.Ok(await mediator.Send(new ListarGranjasQuery()))).RequireAuthorization(politicaGranjas);
        granjas.MapGet("/{id:guid}", async (Guid id, ISender mediator) => Results.Ok(await mediator.Send(new ObtenerGranjaQuery(id)))).RequireAuthorization(politicaGranjas);
        granjas.MapPut("/{id:guid}", async (Guid id, RenombrarGranjaRequest cuerpo, ISender mediator) => { await mediator.Send(new RenombrarGranjaCommand(id, cuerpo.Nombre)); return Results.NoContent(); }).RequireAuthorization(politicaGranjas);
        granjas.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarGranjaCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaGranjas);
        granjas.MapPost("/{granjaId:guid}/galpones", async (Guid granjaId, CrearGalponRequest c, ISender mediator) => { var id = await mediator.Send(new CrearGalponCommand(granjaId, c.Numero, c.CapacidadMaxima, c.GallinasActuales, c.FechaNacimientoLote, c.Descripcion)); return Results.Created($"/galpones/{id}", new { id }); }).RequireAuthorization(politicaGalpones);
        granjas.MapGet("/{granjaId:guid}/galpones", async (Guid granjaId, ISender mediator) => Results.Ok(await mediator.Send(new ListarGalponesPorGranjaQuery(granjaId)))).RequireAuthorization(politicaGalpones);
        var galpones = app.MapGroup("/galpones");
        galpones.MapGet("/{id:guid}", async (Guid id, ISender mediator) => Results.Ok(await mediator.Send(new ObtenerGalponQuery(id)))).RequireAuthorization(politicaGalpones);
        galpones.MapPut("/{id:guid}", async (Guid id, ActualizarGalponRequest c, ISender mediator) => { await mediator.Send(new ActualizarGalponCommand(id, c.Numero, c.Descripcion, c.CapacidadMaxima)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        galpones.MapPut("/{id:guid}/inventario", async (Guid id, InventarioGalponRequest c, ISender mediator) => { await mediator.Send(new AjustarInventarioGalponCommand(id, c.GallinasActuales)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        galpones.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarGalponCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        return app;
    }
    private sealed record CrearGranjaRequest(string Nombre);
    private sealed record RenombrarGranjaRequest(string Nombre);
    private sealed record CrearGalponRequest(string Numero, int CapacidadMaxima, int GallinasActuales, DateOnly FechaNacimientoLote, string? Descripcion);
    private sealed record ActualizarGalponRequest(string Numero, string? Descripcion, int CapacidadMaxima);
    private sealed record InventarioGalponRequest(int GallinasActuales);
}
