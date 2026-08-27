using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Eficiencia;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.Identity.Infrastructure.Autenticacion;
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
        var politicaVacunacion = PoliticasClientes.Para(Funcionalidades.Vacunacion);
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

        // Catálogo global de programas (spec SP7): escritura solo
        // Administrador; lectura con la política CatalogoVacunacion
        // (funcionalidad Vacunacion o rol de plataforma).
        var programasVacunacion = app.MapGroup("/vacunacion/programas");
        programasVacunacion.MapPost("/", async (CrearProgramaVacunacionRequest c, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearProgramaVacunacionCommand(c.Nombre, c.CantidadAves, c.Observaciones));
            return Results.Created($"/vacunacion/programas/{id}", new { id });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        programasVacunacion.MapGet("/", async (bool? incluirInactivos, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarProgramasVacunacionQuery(incluirInactivos ?? false))))
            .RequireAuthorization(PoliticasClientes.CatalogoVacunacion);
        programasVacunacion.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerProgramaVacunacionQuery(id))))
            .RequireAuthorization(PoliticasClientes.CatalogoVacunacion);
        programasVacunacion.MapPut("/{id:guid}", async (Guid id, ActualizarProgramaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new ActualizarProgramaVacunacionCommand(id, c.Nombre, c.CantidadAves, c.Observaciones));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        programasVacunacion.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarProgramaVacunacionCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador);
        // Subida del Excel que reemplaza el cronograma completo. Sin
        // antiforgery: la autenticación es Bearer, no cookie.
        programasVacunacion.MapPost("/{id:guid}/cronograma-excel", async (Guid id, IFormFile archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            await using var contenido = archivo.OpenReadStream();
            var importados = await mediator.Send(new ImportarCronogramaExcelCommand(id, contenido), cancellationToken);
            return Results.Ok(new { itemsImportados = importados });
        }).RequireAuthorization(PoliticasAutorizacion.SoloAdministrador).DisableAntiforgery();

        // Asignar/quitar plan: decisión estructural del cliente (los
        // trabajadores nunca tienen la funcionalidad Galpones).
        galpones.MapPost("/{galponId:guid}/plan-vacunacion", async (Guid galponId, AsignarPlanVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new AsignarPlanVacunacionCommand(galponId, c.ProgramaId));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);
        galpones.MapDelete("/{galponId:guid}/plan-vacunacion", async (Guid galponId, ISender mediator) =>
        {
            await mediator.Send(new QuitarPlanVacunacionCommand(galponId));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);
        galpones.MapGet("/{galponId:guid}/vacunacion/tareas", async (Guid galponId, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarTareasPorGalponQuery(galponId))))
            .RequireAuthorization(politicaVacunacion);

        var vacunacion = app.MapGroup("/vacunacion");
        vacunacion.MapGet("/tareas", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarNotificacionVacunacionQuery())))
            .RequireAuthorization(politicaVacunacion);
        vacunacion.MapPost("/tareas/{id:guid}/completar", async (Guid id, CompletarTareaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new CompletarTareaVacunacionCommand(id, c.FechaAplicacion, c.AvesVacunadas, c.Observaciones));
            return Results.NoContent();
        }).RequireAuthorization(politicaVacunacion);
        // Cancelar: solo cliente (AND de las dos políticas: rol Cliente +
        // funcionalidad Vacunacion del módulo).
        vacunacion.MapPost("/tareas/{id:guid}/cancelar", async (Guid id, CancelarTareaVacunacionRequest c, ISender mediator) =>
        {
            await mediator.Send(new CancelarTareaVacunacionCommand(id, c.Motivo));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasAutorizacion.SoloCliente, politicaVacunacion);

        return app;
    }
    private sealed record CrearProgramaVacunacionRequest(string Nombre, int CantidadAves, string? Observaciones);
    private sealed record ActualizarProgramaVacunacionRequest(string Nombre, int CantidadAves, string? Observaciones);
    private sealed record AsignarPlanVacunacionRequest(Guid ProgramaId);
    private sealed record CompletarTareaVacunacionRequest(DateOnly? FechaAplicacion, int? AvesVacunadas, string? Observaciones);
    private sealed record CancelarTareaVacunacionRequest(string? Motivo);
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
