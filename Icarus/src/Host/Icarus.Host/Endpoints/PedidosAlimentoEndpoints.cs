using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.Notificaciones;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;

namespace Icarus.Host.Endpoints;

// Flujo de pedidos de alimento (spec SP8). El grupo del tenant queda reservado
// a las cuentas con la función PedidoAlimento y el grupo de CAISY a las
// cuentas con GestorPedidoAlimento. Ningún comando mutable admite reintentos
// duplicados: la segunda transición responde 409. Los logs solo llevan ids
// técnicos, estados y conteos (anti-PII).
public static class PedidosAlimentoEndpoints
{
    public static IEndpointRouteBuilder MapPedidosAlimento(this IEndpointRouteBuilder app)
    {
        var politicaTenant = PoliticasClientes.Para(Funcionalidades.PedidoAlimento);
        var politicaCaisy = PoliticasAutorizacion.FuncionalidadCaisy(
            FuncionalidadesCaisy.GestorPedidoAlimento);

        var tenant = app.MapGroup("/pedidos-alimento").RequireAuthorization(politicaTenant);
        tenant.MapPost("/", async (GuardarPedidoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(
                new CrearPedidoAlimentoCommand(ParsearLineas(cuerpo.Detalles)), cancellationToken);
            return Results.Created($"/pedidos-alimento/{id}", new { id });
        });
        tenant.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarPedidosAlimentoQuery(), cancellationToken)));
        // Publicación vigente y cupo semanal para la bandeja del tenant
        // (spec SP8): la publicación es global pero su lectura para pedidos
        // queda autorizada por la función del tenant.
        tenant.MapGet("/precios-vigentes", async Task<IResult> (
            ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioVigenteQuery(null), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });
        tenant.MapGet("/cupo", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerCupoPedidosQuery(), cancellationToken)));
        tenant.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPedidoAlimentoQuery(id), cancellationToken)));
        tenant.MapPut("/{id:guid}", async (Guid id, GuardarPedidoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new EditarPedidoAlimentoCommand(id, ParsearLineas(cuerpo.Detalles)), cancellationToken);
            return Results.NoContent();
        });
        tenant.MapDelete("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DesactivarPedidoAlimentoCommand(id), cancellationToken);
            return Results.NoContent();
        });
        tenant.MapPost("/{id:guid}/enviar", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new EnviarPedidoAlimentoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Sondeo de notificaciones del tenant con ETag y corte por fecha:
        // la UI compone los mensajes y el marcado de lectura es idempotente.
        MapNotificaciones(tenant);

        var caisy = app.MapGroup("/pedidos-alimento-caisy").RequireAuthorization(politicaCaisy);
        caisy.MapGet("/", async (ISender mediator, string? estado, string? presentacion,
            int? pagina, int? tamanoPagina, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(
                new ListarPedidosCaisyQuery(estado, presentacion,
                    pagina ?? 1, tamanoPagina ?? 20),
                cancellationToken)));
        caisy.MapGet("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPedidoAlimentoQuery(id), cancellationToken)));
        caisy.MapPost("/{id:guid}/devolver", async (Guid id, MotivoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DevolverPedidoAlimentoCommand(id, cuerpo.Motivo), cancellationToken);
            return Results.NoContent();
        });
        caisy.MapPost("/{id:guid}/rechazar", async (Guid id, MotivoRequest cuerpo, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new RechazarPedidoAlimentoCommand(id, cuerpo.Motivo), cancellationToken);
            return Results.NoContent();
        });
        caisy.MapPost("/{id:guid}/aceptar", async (Guid id, FechaEntregaRequest cuerpo,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new AceptarPedidoAlimentoCommand(id, cuerpo.FechaEntregaEstimada), cancellationToken);
            return Results.NoContent();
        });
        caisy.MapPost("/{id:guid}/entrega-estimada", async (Guid id, FechaEntregaRequest cuerpo,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new ActualizarEntregaEstimadaPedidoCommand(id, cuerpo.FechaEntregaEstimada),
                cancellationToken);
            return Results.NoContent();
        });

        // Bandeja global de CAISY: mismas notificaciones de su alcance.
        MapNotificaciones(caisy);

        return app;
    }

    private static void MapNotificaciones(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/notificaciones", async Task<IResult> (
            ISender mediator, HttpContext contexto, DateTime? since,
            CancellationToken cancellationToken) =>
        {
            var notificaciones = await mediator.Send(new ListarNotificacionesQuery(), cancellationToken);
            var contador = notificaciones.Count(n => !n.Leida);
            var etag = CalcularEtag(notificaciones, contador);
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
            await mediator.Send(new MarcarNotificacionLeidaCommand(id), cancellationToken);
            return Results.NoContent();
        });
    }

    // La huella cubre la bandeja completa del alcance (fecha más reciente y
    // contador de no leídas): si no cambió, el sondeo responde 304.
    private static string CalcularEtag(IReadOnlyList<NotificacionResumen> notificaciones, int contador)
    {
        var maxima = notificaciones.Count == 0
            ? DateTime.MinValue
            : notificaciones.Max(n => n.FechaUtc);
        var huella = string.Create(CultureInfo.InvariantCulture,
            $"{maxima.Ticks}:{contador}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(huella));
        return $"\"{Convert.ToHexString(bytes)[..16]}\"";
    }

    private static IReadOnlyList<DatosDetallePedido> ParsearLineas(
        IReadOnlyList<LineaPedidoRequest> lineas) =>
        lineas.Select(linea =>
        {
            if (!Enum.TryParse<TipoAlimento>(linea.TipoAlimento, true, out var tipo)
                || !Enum.TryParse<PresentacionAlimento>(linea.Presentacion, true, out var presentacion))
                throw new ValidationException("El tipo o la presentación indicada no existe.");
            return new DatosDetallePedido(tipo, presentacion, linea.Cantidad);
        }).ToList();

    private sealed record LineaPedidoRequest(string TipoAlimento, string Presentacion, int Cantidad);

    private sealed record GuardarPedidoRequest(IReadOnlyList<LineaPedidoRequest> Detalles);

    private sealed record MotivoRequest(string Motivo);

    private sealed record FechaEntregaRequest(DateOnly FechaEntregaEstimada);
}
