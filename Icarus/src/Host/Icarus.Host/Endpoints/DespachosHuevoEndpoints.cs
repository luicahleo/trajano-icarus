using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Despacho de huevo del tenant (spec SP9): reservado a la funcionalidad
// DespachoHuevo. El tenant gestiona Borrador y Despachado; la recepción
// (Recibido, spec SP9C) la confirma CAISY (GestorRecepcionHuevos) desde
// /despachos-huevo-caisy, que también sirve el recibo PDF.
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

        // Crédito disponible del tenant (spec SP9): informativo, no bloquea
        // el envío de pedidos de alimento.
        tenant.MapGet("/credito", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerBalanceCreditoHuevoQuery(), cancellationToken)));

        var politicaCaisy = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorRecepcionHuevos);
        var caisy = app.MapGroup("/despachos-huevo-caisy").RequireAuthorization(politicaCaisy);

        caisy.MapGet("/", async (ISender mediator, string? estado, int? pagina, int? tamanoPagina,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<EstadoDespachoHuevo>(estado, true, out var estadoParseado) && estado is not null)
                return Results.BadRequest(new { error = "El estado indicado no existe." });
            return Results.Ok(await mediator.Send(
                new ListarDespachosHuevoCaisyQuery(
                    estado is null ? null : estadoParseado, pagina ?? 1, tamanoPagina ?? 20),
                cancellationToken));
        });

        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerDespachoHuevoQuery(id), cancellationToken)));

        caisy.MapPost("/{id:guid}/confirmar-recepcion", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new ConfirmarRecepcionDespachoHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        caisy.MapGet("/{id:guid}/recibo.pdf", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var bytes = await mediator.Send(new ObtenerReciboDespachoHuevoPdfQuery(id), cancellationToken);
            return Results.File(bytes, "application/pdf", "recibo.pdf");
        });

        MapNotificacionesDespachoHuevo(tenant);
        MapNotificacionesDespachoHuevo(caisy);

        return app;
    }

    private static void MapNotificacionesDespachoHuevo(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/notificaciones", async Task<IResult> (
            ISender mediator, HttpContext contexto, DateTime? since,
            CancellationToken cancellationToken) =>
        {
            var notificaciones = await mediator.Send(new ListarNotificacionesDespachoHuevoQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones.Select(n => n.FechaUtc), contador);
            if (contexto.Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            contexto.Response.Headers.ETag = etag;
            var visibles = since is { } corte
                ? notificaciones.Where(n => n.FechaUtc > corte).ToList()
                : notificaciones;
            return Results.Ok(new { items = visibles, contador });
        });
        grupo.MapPost("/notificaciones/{id:guid}/marcar-leida", async (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new MarcarNotificacionDespachoHuevoLeidaCommand(id), cancellationToken);
            return Results.NoContent();
        });
    }

    // La huella cubre la bandeja completa del alcance (fecha más reciente y
    // contador de no leídas): si no cambió, el sondeo responde 304.
    private static string CalcularEtag(IEnumerable<DateTime> fechas, int contador)
    {
        var maxima = fechas.DefaultIfEmpty(DateTime.MinValue).Max();
        var huella = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{maxima.Ticks}:{contador}");
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(huella));
        return $"\"{Convert.ToHexString(bytes)[..16]}\"";
    }

    private sealed record GuardarDespachoRequest(IReadOnlyList<LineaDespachoHuevo> Lineas);
}
