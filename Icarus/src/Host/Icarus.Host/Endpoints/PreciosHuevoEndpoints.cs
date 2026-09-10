using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Catálogo global de publicaciones de precio de huevo (spec SP9): reservado a
// las cuentas CAISY con GestorRecepcionHuevos. Sin contenido del documento en
// los logs (anti-PII): solo ids técnicos y conteos.
public static class PreciosHuevoEndpoints
{
    private const long TamanoMaximoExcel = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPreciosHuevo(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorRecepcionHuevos);
        var grupo = app.MapGroup("/precios-huevo-caisy").RequireAuthorization(politica);

        grupo.MapPost("/importar", async Task<IResult> (
            IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta el archivo Excel." });
            if (archivo.Length > TamanoMaximoExcel)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (!string.Equals(Path.GetExtension(archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Solo se acepta un archivo XLSX." });
            await using var contenido = archivo.OpenReadStream();
            var id = await mediator.Send(new ImportarPublicacionPrecioHuevoExcelCommand(contenido), cancellationToken);
            return Results.Created($"/precios-huevo-caisy/{id}", new { id });
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoExcel));

        grupo.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarPublicacionesPrecioHuevoQuery(), cancellationToken)));

        grupo.MapGet("/vigente", async Task<IResult> (
            DateOnly? fecha, ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioHuevoVigenteQuery(fecha), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });

        grupo.MapGet("/corregir/previsualizar", async (Guid erronea, Guid correctiva,
            ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(
                new PrevisualizarCorreccionPrecioHuevoQuery(erronea, correctiva), cancellationToken)));

        grupo.MapPost("/corregir", async (CorregirPublicacionPrecioHuevoVigenteCommand comando,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(comando, cancellationToken);
            return Results.NoContent();
        });

        grupo.MapGet("/{id:guid}", async Task<IResult> (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPublicacionPrecioHuevoQuery(id), cancellationToken)));

        grupo.MapPut("/{id:guid}", async (Guid id, ActualizarBorradorPrecioHuevoCommand comando,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(comando with { PublicacionId = id }, cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/publicar", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new PublicarPublicacionPrecioHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/anular", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new AnularPublicacionPrecioHuevoFuturaCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapDelete("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DescartarBorradorPrecioHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapGet("/{id:guid}/documento-original", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Stream(await mediator.Send(new DescargarDocumentoOriginalPrecioHuevoQuery(id), cancellationToken),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        return app;
    }
}
