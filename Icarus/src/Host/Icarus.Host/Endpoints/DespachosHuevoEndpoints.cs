using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Despacho de huevo del tenant (spec SP9): reservado a la funcionalidad
// DespachoHuevo. Solo Borrador y Despachado; Recibido llega en SP9C.
public static class DespachosHuevoEndpoints
{
    private const long TamanoMaximoImagen = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDespachosHuevo(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasClientes.Para(Funcionalidades.DespachoHuevo);
        var tenant = app.MapGroup("/despachos-huevo").RequireAuthorization(politica);

        tenant.MapPost("/", async (GuardarDespachoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(
                new CrearBorradorDespachoHuevoCommand(cuerpo.Lineas), cancellationToken);
            return Results.Created($"/despachos-huevo/{id}", new { id });
        });

        tenant.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarDespachosHuevoTenantQuery(), cancellationToken)));

        // Precio vigente para la PWA (spec SP9): el catálogo en sí solo lo
        // administra CAISY (`/precios-huevo-caisy`, SP9A), pero el tenant
        // necesita conocer el precio antes de despachar. Mismo patrón que
        // `tenant.MapGet("/precios-vigentes", ...)` en
        // PedidosAlimentoEndpoints.cs.
        tenant.MapGet("/precios-vigentes", async Task<IResult> (
            ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioHuevoVigenteQuery(null), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });

        tenant.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoQuery(id), cancellationToken)));

        tenant.MapPut("/{id:guid}", async (Guid id, GuardarDespachoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new EditarBorradorDespachoHuevoCommand(id, cuerpo.Lineas), cancellationToken);
            return Results.NoContent();
        });

        tenant.MapDelete("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DesactivarBorradorDespachoHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Despachar: foto obligatoria de la nota de entrega en la misma
        // operación (spec SP9), sin antiforgery porque la autenticación es
        // Bearer, no cookie.
        tenant.MapPost("/{id:guid}/despachar", async Task<IResult> (
            Guid id, IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta la foto de la nota de entrega." });
            if (archivo.Length > TamanoMaximoImagen)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await using var contenido = archivo.OpenReadStream();
            await mediator.Send(
                new DespacharDespachoHuevoCommand(id, contenido, archivo.FileName), cancellationToken);
            return Results.NoContent();
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoImagen));

        return app;
    }

    private sealed record GuardarDespachoRequest(IReadOnlyList<LineaDespachoHuevo> Lineas);
}
