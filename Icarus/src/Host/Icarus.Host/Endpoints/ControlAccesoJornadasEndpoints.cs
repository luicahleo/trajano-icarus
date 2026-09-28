using Icarus.ControlAcceso.Application.Jornadas;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.Host.Autorizacion;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class ControlAccesoJornadasEndpoints
{
    public static IEndpointRouteBuilder MapControlAccesoJornadas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso");

        grupo.MapGet("/jornadas", async (
            Guid? trabajadorId,
            DateOnly? desde,
            DateOnly? hasta,
            int? pagina,
            int? tamanoPagina,
            ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarJornadasQuery(
                trabajadorId, desde, hasta, pagina ?? 1, tamanoPagina ?? 20))))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapGet("/jornadas/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerJornadaQuery(id))))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/jornadas/{id:guid}/correcciones", async (
            Guid id, CorregirJornadaRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new CorregirJornadaCommand(
                id, cuerpo.VersionEsperada, cuerpo.Motivo, cuerpo.Valores));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/marcaciones/manuales", async (
            RegistrarMarcacionManualRequest cuerpo, ISender mediator) =>
        {
            var jornadaId = await mediator.Send(new RegistrarMarcacionManualCommand(
                cuerpo.TrabajadorId,
                cuerpo.Tipo,
                cuerpo.HoraDeclaradaUtc,
                cuerpo.Motivo,
                cuerpo.ClaveIdempotencia));
            return Results.Created(
                $"/api/control-acceso/jornadas/{jornadaId}", new { jornadaId });
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        return app;
    }

    private sealed record CorregirJornadaRequest(
        int VersionEsperada,
        string Motivo,
        IReadOnlyList<ValorCorregido> Valores);

    private sealed record RegistrarMarcacionManualRequest(
        Guid TrabajadorId,
        TipoMarcacion Tipo,
        DateTimeOffset HoraDeclaradaUtc,
        string Motivo,
        Guid ClaveIdempotencia);
}
