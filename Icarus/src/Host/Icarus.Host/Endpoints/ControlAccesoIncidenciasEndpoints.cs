using Icarus.ControlAcceso.Application.Incidencias;
using Icarus.ControlAcceso.Domain;
using Icarus.Host.Autorizacion;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class ControlAccesoIncidenciasEndpoints
{
    public static IEndpointRouteBuilder MapControlAccesoIncidencias(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso");

        grupo.MapGet("/incidencias", async (
            EstadoIncidenciaAcceso? estado,
            int? pagina,
            int? tamanoPagina,
            ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarIncidenciasAccesoQuery(
                estado, pagina ?? 1, tamanoPagina ?? 25))))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapGet("/incidencias/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerIncidenciaAccesoQuery(id))))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/incidencias/{id:guid}/resolucion", async (
            Guid id, ResolverIncidenciaRequest cuerpo, ISender mediator) =>
        {
            _ = await mediator.Send(new ResolverIncidenciaAccesoCommand(
                id,
                cuerpo.TrabajadorId,
                cuerpo.Tipo,
                cuerpo.HoraDeclaradaUtc,
                cuerpo.Motivo,
                cuerpo.ClaveIdempotencia,
                cuerpo.VersionEsperada));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/incidencias/{id:guid}/descarte", async (
            Guid id, DescartarIncidenciaRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new DescartarIncidenciaAccesoCommand(
                id, cuerpo.Motivo, cuerpo.ClaveIdempotencia, cuerpo.VersionEsperada));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        return app;
    }

    private sealed record ResolverIncidenciaRequest(
        Guid TrabajadorId,
        TipoMarcacion Tipo,
        DateTimeOffset HoraDeclaradaUtc,
        string Motivo,
        Guid ClaveIdempotencia,
        int? VersionEsperada = null);

    private sealed record DescartarIncidenciaRequest(
        string Motivo,
        Guid ClaveIdempotencia,
        int? VersionEsperada = null);
}
