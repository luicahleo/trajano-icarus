using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Catálogo global de Notificaciones de Precios de Alimentos (spec SP8): la
// consulta y la publicación quedan reservadas a las cuentas CAISY con la
// función GestorPedidoAlimento. Ningún endpoint lleva contenido del documento
// a los logs (anti-PII): solo ids técnicos y conteos.
public static class PreciosAlimentosEndpoints
{
    private const long TamanoMaximoPdf = 20 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPreciosAlimentos(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorPedidoAlimento);
        var grupo = app.MapGroup("/precios-alimentos").RequireAuthorization(politica);

        // Subida del PDF original. Sin antiforgery: la autenticación es
        // Bearer, no cookie. El límite explícito protege la memoria del Host
        // aunque el servidor no imponga el tope del cuerpo.
        grupo.MapPost("/importar", async Task<IResult> (
            IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta el archivo PDF." });
            if (archivo.Length > TamanoMaximoPdf)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await using var contenido = archivo.OpenReadStream();
            var id = await mediator.Send(new ImportarNotificacionPdfCommand(contenido), cancellationToken);
            return Results.Created($"/precios-alimentos/{id}", new { id });
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoPdf));

        grupo.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarNotificacionesPreciosQuery(), cancellationToken)));

        grupo.MapGet("/vigente", async Task<IResult> (
            DateOnly? fecha, ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioVigenteQuery(fecha), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });

        grupo.MapGet("/{id:guid}", async Task<IResult> (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerNotificacionPreciosQuery(id), cancellationToken)));

        grupo.MapPut("/{id:guid}", async (Guid id, ActualizarBorradorPreciosCommand comando,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(comando with { NotificacionId = id }, cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/publicar", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new PublicarNotificacionPreciosCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/anular", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new AnularNotificacionFuturaCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapGet("/{id:guid}/documento-original", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Stream(await mediator.Send(new DescargarDocumentoOriginalQuery(id), cancellationToken),
                "application/pdf"));

        return app;
    }
}
