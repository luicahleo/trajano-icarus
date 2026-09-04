using System.Globalization;
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PedidosAlimento;

public sealed record CrearPedidoAlimentoCommand(IReadOnlyList<DatosDetallePedido> Detalles)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.crear",
        new Dictionary<string, DatoRegistroVuelo> { ["Lineas"] = DatoRegistroVuelo.Entero });
}

public sealed record EditarPedidoAlimentoCommand(
    Guid PedidoId, IReadOnlyList<DatosDetallePedido> Detalles)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.editar",
        new Dictionary<string, DatoRegistroVuelo> { ["Lineas"] = DatoRegistroVuelo.Entero });
}

public sealed record DesactivarPedidoAlimentoCommand(Guid PedidoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.desactivar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record EnviarPedidoAlimentoCommand(Guid PedidoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.enviar",
        new Dictionary<string, DatoRegistroVuelo> { ["Lineas"] = DatoRegistroVuelo.Entero });
}

public sealed record ListarPedidosAlimentoQuery
    : IRequest<IReadOnlyList<PedidoAlimentoResumen>>;

public sealed record ObtenerPedidoAlimentoQuery(Guid PedidoId)
    : IRequest<PedidoAlimentoDetalle>;

public sealed record PedidoAlimentoResumen(
    Guid Id, string Estado, string Presentacion, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado, int CantidadLineas);

public sealed record LineaPedidoAlimentoResumen(
    Guid Id, string TipoAlimento, string Presentacion, int CantidadSolicitada,
    int Equivalentes40Kg, decimal? PrecioFinalPor40Kg, decimal? SubtotalSolicitado);

public sealed record TransicionPedidoAlimentoResumen(
    string EstadoOrigen, string EstadoDestino, DateTime FechaUtc,
    string? Motivo, DateOnly? FechaEntregaEstimada);

public sealed record PedidoAlimentoDetalle(
    Guid Id, Guid ClienteId, string Estado, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado,
    IReadOnlyList<LineaPedidoAlimentoResumen> Lineas,
    IReadOnlyList<TransicionPedidoAlimentoResumen> Historial);

public sealed class CrearPedidoAlimentoValidator : AbstractValidator<CrearPedidoAlimentoCommand>
{
    public CrearPedidoAlimentoValidator() => RuleFor(c => c.Detalles).NotNull().NotEmpty();
}

public sealed class EditarPedidoAlimentoValidator : AbstractValidator<EditarPedidoAlimentoCommand>
{
    public EditarPedidoAlimentoValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.Detalles).NotNull().NotEmpty();
    }
}

// El borrador es compartido del tenant (spec SP8): cualquier cuenta del tenant
// con la función PedidoAlimento puede crear; el creador queda como auditoría.
public sealed class CrearPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CrearPedidoAlimentoCommand, Guid>
{
    public async Task<Guid> Handle(CrearPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("Solo una cuenta de tenant puede crear pedidos.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = new PedidoAlimento(clienteId, actorId, request.Detalles);
        repositorio.Agregar(pedido);
        registroVuelo.Decidir("avicola.pedidos.crear", "creacion", "aplicada",
            new Dictionary<string, object?> { ["Lineas"] = pedido.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return pedido.Id;
    }
}

public sealed class EditarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<EditarPedidoAlimentoCommand>
{
    public async Task Handle(EditarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Borrador)
            throw new ConflictException("Solo un borrador se puede editar.");
        pedido.EditarDetalles(request.Detalles);
        // Las líneas se recrean con clave nueva: se registran como Added.
        foreach (var linea in pedido.Detalles)
            repositorio.AgregarDetalle(linea);
        registroVuelo.Decidir("avicola.pedidos.editar", "edicion", "aplicada",
            new Dictionary<string, object?> { ["Lineas"] = pedido.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DesactivarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarPedidoAlimentoCommand>
{
    public async Task Handle(DesactivarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Borrador)
            throw new ConflictException("Solo un borrador se puede desactivar.");
        pedido.Desactivar();
        registroVuelo.Decidir("avicola.pedidos.desactivar", "borrado", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Envío a CAISY (spec SP8): en una única transacción se comprueba el cupo con
// una consulta bloqueable, se congela la publicación vigente resuelta por la
// fecha de negocio de Bolivia y se registra la transición. Si falta precio
// para una línea o no hay publicación vigente, el envío falla completo y el
// borrador queda intacto. Los dobles clics y reintentos chocan con el estado
// y responden 409 sin gastar cupo ni repetir la transición.
public sealed class EnviarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    IRepositorioNotificacionesPrecios repositorioPrecios,
    OpcionesPedidosAlimento opciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<EnviarPedidoAlimentoCommand>
{
    public async Task Handle(EnviarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Borrador)
            throw new ConflictException("Solo un pedido en borrador se puede enviar.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        await using var transaccion = await repositorio.IniciarTransaccionAsync(cancellationToken);
        var inicioSemana = InicioSemanaIso(hoy);
        var enviados = await repositorio.ContarEnviadosEnSemanaBloqueandoAsync(
            pedido.ClienteId, inicioSemana, inicioSemana.AddDays(6), cancellationToken);
        var maximo = opciones.MaximoPorSemana;
        if (enviados >= maximo)
            throw new ConflictException(
                $"Se alcanzó el límite semanal de {maximo.ToString(CultureInfo.InvariantCulture)} pedidos enviados.");

        var vigente = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken)
            ?? throw new ValidationException("No hay una publicación de precios vigente.");
        var precios = vigente.Detalles
            .Select(d => new DatosPrecioEnvio(d.TipoAlimento, d.Presentacion, d.PrecioFinalPor40Kg, vigente.Id))
            .ToList();
        pedido.EnviarACaisy(hoy, actorId, precios);
        registroVuelo.Decidir("avicola.pedidos.enviar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = pedido.Detalles.Count,
                ["NotificacionPreciosId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        await transaccion.ConfirmarAsync(cancellationToken);
    }

    // Semana ISO: la semana empieza el lunes.
    internal static DateOnly InicioSemanaIso(DateOnly fecha) =>
        fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7));
}

public sealed class ListarPedidosAlimentoHandler(IRepositorioPedidosAlimento repositorio)
    : IRequestHandler<ListarPedidosAlimentoQuery, IReadOnlyList<PedidoAlimentoResumen>>
{
    public async Task<IReadOnlyList<PedidoAlimentoResumen>> Handle(
        ListarPedidosAlimentoQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarAsync(cancellationToken))
            .OrderByDescending(p => p.FechaPedido)
            .ThenByDescending(p => p.Id)
            .Select(MapeadorPedidos.MapearResumen)
            .ToList();
}

public sealed class ObtenerPedidoAlimentoHandler(IRepositorioPedidosAlimento repositorio)
    : IRequestHandler<ObtenerPedidoAlimentoQuery, PedidoAlimentoDetalle>
{
    public async Task<PedidoAlimentoDetalle> Handle(
        ObtenerPedidoAlimentoQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerConHistorialAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        return MapeadorPedidos.MapearDetalle(pedido);
    }
}

internal static class MapeadorPedidos
{
    public static PedidoAlimentoResumen MapearResumen(PedidoAlimento pedido) =>
        new(pedido.Id, pedido.Estado.ToString(), pedido.Detalles.First().Presentacion.ToString(),
            pedido.FechaPedido, pedido.FechaEntregaEstimada, pedido.TotalSolicitado,
            pedido.Detalles.Count);

    public static PedidoAlimentoDetalle MapearDetalle(PedidoAlimento pedido) =>
        new(pedido.Id, pedido.ClienteId, pedido.Estado.ToString(), pedido.FechaPedido,
            pedido.FechaEntregaEstimada, pedido.TotalSolicitado,
            pedido.Detalles
                .Select(d => new LineaPedidoAlimentoResumen(
                    d.Id, d.TipoAlimento.ToString(), d.Presentacion.ToString(),
                    d.CantidadSolicitada, d.Equivalentes40Kg,
                    d.PrecioFinalPor40Kg, d.SubtotalSolicitado))
                .ToList(),
            pedido.Historial
                .Select(t => new TransicionPedidoAlimentoResumen(
                    t.EstadoOrigen.ToString(), t.EstadoDestino.ToString(), t.FechaUtc,
                    t.Motivo, t.FechaEntregaEstimada))
                .ToList());
}
