using Icarus.ControlAcceso.Application.Incidencias;
using Icarus.ControlAcceso.Application.Notificaciones;
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

        grupo.MapGet("/notificaciones", async (
            ISender mediator, HttpContext contexto, DateTime? since,
            CancellationToken cancellationToken) =>
        {
            var notificaciones = await mediator.Send(new ListarNotificacionesAccesoQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones, contador);
            contexto.Response.Headers.CacheControl = "no-cache";
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            contexto.Response.Headers.ETag = etag;
            var visibles = since is { } corte
                ? notificaciones.Where(n => n.FechaUtc > corte).ToList()
                : notificaciones;
            return Results.Ok(new { items = visibles, contador });
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/notificaciones/{id:guid}/marcar-leida", async (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new MarcarNotificacionAccesoLeidaCommand(id), cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        return app;
    }

    private static string CalcularEtag(IReadOnlyList<NotificacionAccesoResumen> notificaciones, int contador)
    {
        var maxima = notificaciones.Count == 0
            ? DateTime.MinValue
            : notificaciones.Max(n => n.FechaUtc);
        var huella = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{maxima.Ticks}:{contador}");
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(huella));
        return $"\"{Convert.ToHexString(bytes)[..16]}\"";
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
