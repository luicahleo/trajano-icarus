using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Catálogo global de programas de vacunación (spec 2026-10-30-gestor-
// veterinario-vacunacion): reservado a las cuentas CAISY con
// GestorVeterinario. Ciclo Borrador -> Publicado; el Administrador de
// plataforma ya no participa. Este grupo solo toca el catálogo: el gestor no
// tiene tenant y el filtro EF de tenant es permisivo sin ClienteId, así que
// nunca debe exponer galpones, clientes ni tareas. Sin contenido del Excel en
// los logs (anti-PII): solo ids técnicos y conteos.
public static class ProgramasVacunacionCaisyEndpoints
{
    private const long TamanoMaximoExcel = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapProgramasVacunacionCaisy(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorVeterinario);
        var grupo = app.MapGroup("/programas-vacunacion-caisy").RequireAuthorization(politica);

        grupo.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarProgramasVacunacionGestionQuery(), cancellationToken)));

        grupo.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerProgramaVacunacionGestionQuery(id), cancellationToken)));

        grupo.MapPost("/", async (DatosProgramaVacunacionRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(
                new CrearProgramaVacunacionCommand(cuerpo.Nombre, cuerpo.CantidadAves, cuerpo.Observaciones),
                cancellationToken);
            return Results.Created($"/programas-vacunacion-caisy/{id}", new { id });
        });

        grupo.MapPut("/{id:guid}", async (Guid id, DatosProgramaVacunacionRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new ActualizarProgramaVacunacionCommand(id, cuerpo.Nombre, cuerpo.CantidadAves, cuerpo.Observaciones),
                cancellationToken);
            return Results.NoContent();
        });

        // Reemplaza el cronograma del borrador. Sin antiforgery: la
        // autenticación es Bearer, no cookie. El límite explícito protege la
        // memoria del Host.
        grupo.MapPost("/{id:guid}/cronograma-excel", async Task<IResult> (
            Guid id, IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta el archivo Excel." });
            if (archivo.Length > TamanoMaximoExcel)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (!string.Equals(Path.GetExtension(archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Solo se acepta un archivo XLSX." });
            await using var contenido = archivo.OpenReadStream();
            var importados = await mediator.Send(new ImportarCronogramaExcelCommand(id, contenido), cancellationToken);
            return Results.Ok(new { itemsImportados = importados });
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoExcel));

        grupo.MapPost("/{id:guid}/publicar", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new PublicarProgramaVacunacionCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Borrado lógico de un borrador (glosario): un publicado responde 400.
        grupo.MapDelete("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new EliminarBorradorProgramaVacunacionCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Solo un publicado: desactiva sus tareas pendientes en todos los galpones.
        grupo.MapPost("/{id:guid}/desactivar", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DesactivarProgramaVacunacionCommand(id), cancellationToken);
            return Results.NoContent();
        });

        return app;
    }

    private sealed record DatosProgramaVacunacionRequest(string Nombre, int CantidadAves, string? Observaciones);
}
