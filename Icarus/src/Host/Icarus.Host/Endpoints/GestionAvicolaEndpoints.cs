using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Eficiencia;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class GestionAvicolaEndpoints
{
    public static IEndpointRouteBuilder MapGestionAvicola(this IEndpointRouteBuilder app)
    {
        var politicaGranjas = PoliticasClientes.Para(Funcionalidades.Granjas);
        var politicaGalpones = PoliticasClientes.Para(Funcionalidades.Galpones);
        var politicaProduccion = PoliticasClientes.Para(Funcionalidades.ProduccionHuevos);
        var politicaMortalidad = PoliticasClientes.Para(Funcionalidades.Mortalidad);
        const string politicaEstructura = "GestionAvicolaEstructura";
        var granjas = app.MapGroup("/granjas");
        granjas.MapPost("/", async (CrearGranjaRequest cuerpo, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearGranjaCommand(cuerpo.Nombre));
            return Results.Created($"/granjas/{id}", new { id });
        }).RequireAuthorization(politicaGranjas);
        granjas.MapGet("/", async (ISender mediator) => Results.Ok(await mediator.Send(new ListarGranjasQuery()))).RequireAuthorization(politicaEstructura);
        granjas.MapGet("/{id:guid}", async (Guid id, ISender mediator) => Results.Ok(await mediator.Send(new ObtenerGranjaQuery(id)))).RequireAuthorization(politicaEstructura);
        granjas.MapPut("/{id:guid}", async (Guid id, RenombrarGranjaRequest cuerpo, ISender mediator) => { await mediator.Send(new RenombrarGranjaCommand(id, cuerpo.Nombre)); return Results.NoContent(); }).RequireAuthorization(politicaGranjas);
        granjas.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarGranjaCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaGranjas);
        granjas.MapPost("/{granjaId:guid}/galpones", async (Guid granjaId, CrearGalponRequest c, ISender mediator) => { var id = await mediator.Send(new CrearGalponCommand(granjaId, c.Numero, c.CapacidadMaxima, c.GallinasActuales, c.FechaNacimientoLote, c.Descripcion)); return Results.Created($"/galpones/{id}", new { id }); }).RequireAuthorization(politicaGalpones);
        granjas.MapGet("/{granjaId:guid}/galpones", async (Guid granjaId, ISender mediator) => Results.Ok(await mediator.Send(new ListarGalponesPorGranjaQuery(granjaId)))).RequireAuthorization(politicaEstructura);
        var galpones = app.MapGroup("/galpones");
        galpones.MapGet("/{id:guid}", async (Guid id, ISender mediator) => Results.Ok(await mediator.Send(new ObtenerGalponQuery(id)))).RequireAuthorization(politicaEstructura);
        galpones.MapPut("/{id:guid}", async (Guid id, ActualizarGalponRequest c, ISender mediator) => { await mediator.Send(new ActualizarGalponCommand(id, c.Numero, c.Descripcion, c.CapacidadMaxima)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        galpones.MapPut("/{id:guid}/inventario", async (Guid id, InventarioGalponRequest c, ISender mediator) => { await mediator.Send(new AjustarInventarioGalponCommand(id, c.GallinasActuales)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        galpones.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarGalponCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaGalpones);
        galpones.MapPost("/{galponId:guid}/produccion", async (Guid galponId, RegistrarProduccionRequest c, ISender mediator) => { var id = await mediator.Send(new RegistrarProduccionCommand(galponId, c.Hora, c.CantidadMaples, c.UnidadesIncompletas, c.MaplesDescarte, c.UnidadesDescarte, c.IdempotencyKey)); return Results.Created($"/produccion/{id}", new { id }); }).RequireAuthorization(politicaProduccion);
        galpones.MapGet("/{galponId:guid}/produccion", async (Guid galponId, DateOnly? fecha, ISender mediator) => Results.Ok(await mediator.Send(new ListarProduccionPorDiaQuery(galponId, fecha)))).RequireAuthorization(politicaProduccion);
        galpones.MapGet("/{galponId:guid}/eficiencia", async (Guid galponId, DateOnly? desde, DateOnly? hasta, ISender mediator) => Results.Ok(await mediator.Send(new ObtenerEficienciaGalponQuery(galponId, desde, hasta)))).RequireAuthorization(politicaProduccion);
        galpones.MapPost("/{galponId:guid}/mortalidad", async (Guid galponId, RegistrarMortalidadRequest c, ISender mediator) => { var id = await mediator.Send(new RegistrarMortalidadCommand(galponId, c.Hora, c.CantidadMuertas, c.IdempotencyKey)); return Results.Created($"/mortalidad/{id}", new { id }); }).RequireAuthorization(politicaMortalidad);
        galpones.MapGet("/{galponId:guid}/mortalidad", async (Guid galponId, DateOnly? fecha, ISender mediator) => Results.Ok(await mediator.Send(new ListarMortalidadPorDiaQuery(galponId, fecha)))).RequireAuthorization(politicaMortalidad);
        var produccion = app.MapGroup("/produccion");
        produccion.MapPut("/{id:guid}", async (Guid id, EditarProduccionRequest c, ISender mediator) => { await mediator.Send(new EditarProduccionCommand(id, c.Hora, c.CantidadMaples, c.UnidadesIncompletas, c.MaplesDescarte, c.UnidadesDescarte)); return Results.NoContent(); }).RequireAuthorization(politicaProduccion);
        produccion.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarProduccionCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaProduccion);
        var mortalidad = app.MapGroup("/mortalidad");
        mortalidad.MapPut("/{id:guid}", async (Guid id, EditarMortalidadRequest c, ISender mediator) => { await mediator.Send(new EditarMortalidadCommand(id, c.Hora, c.CantidadMuertas)); return Results.NoContent(); }).RequireAuthorization(politicaMortalidad);
        mortalidad.MapDelete("/{id:guid}", async (Guid id, ISender mediator) => { await mediator.Send(new DesactivarMortalidadCommand(id)); return Results.NoContent(); }).RequireAuthorization(politicaMortalidad);
        return app;
    }
    private sealed record CrearGranjaRequest(string Nombre);
    private sealed record RenombrarGranjaRequest(string Nombre);
    private sealed record CrearGalponRequest(string Numero, int CapacidadMaxima, int GallinasActuales, DateOnly FechaNacimientoLote, string? Descripcion);
    private sealed record ActualizarGalponRequest(string Numero, string? Descripcion, int CapacidadMaxima);
    private sealed record InventarioGalponRequest(int GallinasActuales);
    private sealed record RegistrarProduccionRequest(TimeOnly? Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte, Guid? IdempotencyKey);
    private sealed record EditarProduccionRequest(TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte);
    private sealed record RegistrarMortalidadRequest(TimeOnly? Hora, int CantidadMuertas, Guid? IdempotencyKey);
    private sealed record EditarMortalidadRequest(TimeOnly Hora, int CantidadMuertas);
}
