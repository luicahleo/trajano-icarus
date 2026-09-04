using System.Globalization;
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.Notificaciones;
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

public sealed record DevolverPedidoAlimentoCommand(Guid PedidoId, string Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.devolver", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record RechazarPedidoAlimentoCommand(Guid PedidoId, string Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.rechazar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record AceptarPedidoAlimentoCommand(Guid PedidoId, DateOnly FechaEntregaEstimada)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.aceptar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ActualizarEntregaEstimadaPedidoCommand(Guid PedidoId, DateOnly NuevaFecha)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.actualizar-entrega", new Dictionary<string, DatoRegistroVuelo>());
}

// Despacho (spec SP8C): una única entrega y una única nota por pedido,
// registrada por CAISY con datos manuales. El número de nota y las líneas no
// se registran en el vuelo (anti-PII): solo ids técnicos, estado y conteos.
public sealed record RegistrarDespachoPedidoCommand(
    Guid PedidoId, string NumeroNota, DateOnly FechaNota,
    decimal? TotalInformado, IReadOnlyList<DatosLineaEntrega> LineasEntregadas)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.despachar", new Dictionary<string, DatoRegistroVuelo>
        { ["Lineas"] = DatoRegistroVuelo.Entero });
}

public sealed record ListarPedidosAlimentoQuery
    : IRequest<IReadOnlyList<PedidoAlimentoResumen>>;

public sealed record ObtenerPedidoAlimentoQuery(Guid PedidoId)
    : IRequest<PedidoAlimentoDetalle>;

// Cupo semanal visible en la bandeja del tenant (spec SP8): pedidos enviados
// en la semana ISO actual contra el máximo configurado.
public sealed record ObtenerCupoPedidosQuery : IRequest<CupoPedidosResumen>;

public sealed record CupoPedidosResumen(int Enviados, int Maximo, DateOnly Desde, DateOnly Hasta);

public sealed record ListarPedidosCaisyQuery(
    string? Estado, string? Presentacion, int Pagina, int TamanoPagina)
    : IRequest<PaginaPedidosCaisy>;
public sealed record PedidoCaisyResumen(
    Guid Id, Guid ClienteId, string Estado, string Presentacion, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado, int CantidadLineas);

public sealed record PaginaPedidosCaisy(
    IReadOnlyList<PedidoCaisyResumen> Items, int Total, int Pagina, int TamanoPagina);

public sealed class ListarPedidosCaisyValidator : AbstractValidator<ListarPedidosCaisyQuery>
{
    public ListarPedidosCaisyValidator()
    {
        RuleFor(c => c.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(c => c.TamanoPagina).InclusiveBetween(1, 100);
        RuleFor(c => c.Estado)
            .Must(e => e is null || Enum.TryParse<EstadoPedidoAlimento>(e, true, out _))
            .WithMessage("El estado indicado no existe.");
        RuleFor(c => c.Presentacion)
            .Must(p => p is null || Enum.TryParse<PresentacionAlimento>(p, true, out _))
            .WithMessage("La presentación indicada no existe.");
    }
}

public sealed record PedidoAlimentoResumen(
    Guid Id, string Estado, string Presentacion, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado, int CantidadLineas);

public sealed record LineaPedidoAlimentoResumen(
    Guid Id, string TipoAlimento, string Presentacion, int CantidadSolicitada,
    int Equivalentes40Kg, decimal? PrecioFinalPor40Kg, decimal? SubtotalSolicitado,
    Guid? NotificacionPreciosAlimentosId);

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

public sealed class DevolverPedidoAlimentoValidator : AbstractValidator<DevolverPedidoAlimentoCommand>
{
    public DevolverPedidoAlimentoValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
}

public sealed class RechazarPedidoAlimentoValidator : AbstractValidator<RechazarPedidoAlimentoCommand>
{
    public RechazarPedidoAlimentoValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
}

public sealed class AceptarPedidoAlimentoValidator : AbstractValidator<AceptarPedidoAlimentoCommand>
{
    public AceptarPedidoAlimentoValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.FechaEntregaEstimada).NotEmpty();
    }
}

public sealed class ActualizarEntregaEstimadaPedidoValidator
    : AbstractValidator<ActualizarEntregaEstimadaPedidoCommand>
{
    public ActualizarEntregaEstimadaPedidoValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.NuevaFecha).NotEmpty();
    }
}

public sealed class RegistrarDespachoPedidoValidator
    : AbstractValidator<RegistrarDespachoPedidoCommand>
{
    public RegistrarDespachoPedidoValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.NumeroNota).NotEmpty().MaximumLength(100);
        RuleFor(c => c.FechaNota).NotEmpty();
        RuleFor(c => c.LineasEntregadas).NotNull().NotEmpty();
        RuleForEach(c => c.LineasEntregadas)
            .Must(l => l.CantidadEntregada >= 0)
            .WithMessage("La cantidad entregada no puede ser negativa.");
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
// fecha de negocio de Bolivia y se registra la transición junto con la
// notificación para la bandeja CAISY. Si falta precio para una línea o no hay
// publicación vigente, el envío falla completo y el borrador queda intacto.
// Los dobles clics y reintentos chocan con el estado y responden 409 sin
// gastar cupo ni repetir la transición ni la notificación.
public sealed class EnviarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    IRepositorioNotificacionesPrecios repositorioPrecios,
    OpcionesPedidosAlimento opciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
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
        var inicioSemana = SemanasIso.Inicio(hoy);
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
        // El reenvío tras una devolución avisa a CAISY con su propio tipo: la
        // primera salida del borrador fija FechaPedido y la devolución la
        // conserva (el historial no se carga en este comando).
        var esReenvio = pedido.FechaPedido is not null;
        pedido.EnviarACaisy(hoy, actorId, precios);
        notificaciones.Agregar(NotificacionInterna.ParaCaisy(
            esReenvio ? TipoNotificacionPedido.PedidoReenviado : TipoNotificacionPedido.PedidoSolicitado,
            pedido.Id));
        registroVuelo.Decidir("avicola.pedidos.enviar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = pedido.Detalles.Count,
                ["NotificacionPreciosId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        await transaccion.ConfirmarAsync(cancellationToken);
    }
}

// Semana ISO: la semana empieza el lunes (spec SP8, límite semanal).
internal static class SemanasIso
{
    public static DateOnly Inicio(DateOnly fecha) =>
        fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7));
}

// Procesamiento CAISY (spec SP8): devolver con motivo, rechazar con motivo,
// aceptar con fecha de entrega estimada y actualizar la fecha sobre un pedido
// aceptado. CAISY nunca altera tipos ni cantidades; cada decisión registra la
// transición y crea exactamente una notificación para la bandeja del tenant
// en la misma transacción local. Los motivos viven solo en el historial.
public sealed class DevolverPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<DevolverPedidoAlimentoCommand>
{
    public async Task Handle(DevolverPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Solicitado)
            throw new ConflictException("Solo un pedido solicitado se puede devolver.");
        pedido.DevolverParaCorreccion(request.Motivo.Trim(), actorId);
        notificaciones.Agregar(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoDevuelto, pedido.Id, pedido.ClienteId));
        registroVuelo.Decidir("avicola.pedidos.devolver", "devolucion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RechazarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<RechazarPedidoAlimentoCommand>
{
    public async Task Handle(RechazarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Solicitado)
            throw new ConflictException("Solo un pedido solicitado se puede rechazar.");
        pedido.Rechazar(request.Motivo.Trim(), actorId);
        notificaciones.Agregar(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoRechazado, pedido.Id, pedido.ClienteId));
        registroVuelo.Decidir("avicola.pedidos.rechazar", "rechazo", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AceptarPedidoAlimentoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<AceptarPedidoAlimentoCommand>
{
    public async Task Handle(AceptarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Solicitado)
            throw new ConflictException("Solo un pedido solicitado se puede aceptar.");
        pedido.Aceptar(request.FechaEntregaEstimada, FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoAceptado, pedido.Id, pedido.ClienteId));
        registroVuelo.Decidir("avicola.pedidos.aceptar", "aceptacion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ActualizarEntregaEstimadaPedidoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<ActualizarEntregaEstimadaPedidoCommand>
{
    public async Task Handle(
        ActualizarEntregaEstimadaPedidoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Aceptado)
            throw new ConflictException("Solo un pedido aceptado permite actualizar la entrega estimada.");
        pedido.ActualizarEntregaEstimada(request.NuevaFecha, FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.EntregaEstimadaActualizada, pedido.Id, pedido.ClienteId,
            Meta(request.NuevaFecha)));
        registroVuelo.Decidir("avicola.pedidos.actualizar-entrega", "actualizacion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // Metadatos técnicos (spec SP8): la fecha nueva en texto invariante; el
    // mensaje visible lo compone la UI.
    private static string Meta(DateOnly nuevaFecha) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{{\"fechaEntregaEstimada\":\"{nuevaFecha}\"}}");
}

// Despacho (spec SP8C "Despacho, nota y recepción"): lo registra CAISY sobre
// un pedido aceptado, con una única entrega y una única nota. La fecha de
// despacho es la fecha de negocio del servidor; el agregado valida nota y
// líneas. La notificación de despachado llega a la bandeja del tenant en la
// misma transacción local. Los reintentos chocan con el estado y responden
// 409 sin repetir la transición ni la notificación.
public sealed class RegistrarDespachoPedidoHandler(
    IRepositorioPedidosAlimento repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<RegistrarDespachoPedidoCommand>
{
    public async Task Handle(RegistrarDespachoPedidoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Aceptado)
            throw new ConflictException("Solo un pedido aceptado se puede despachar.");
        pedido.RegistrarDespacho(
            request.NumeroNota, request.FechaNota, request.TotalInformado,
            request.LineasEntregadas, FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoDespachado, pedido.Id, pedido.ClienteId));
        registroVuelo.Decidir("avicola.pedidos.despachar", "despacho", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = request.LineasEntregadas.Count,
                ["Diferencias"] = ContarDiferencias(pedido),
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // Conteo técnico de líneas con diferencia contra lo solicitado: sin
    // números de nota ni nombres de archivo (anti-PII).
    private static int ContarDiferencias(PedidoAlimento pedido) =>
        pedido.Entrega!.Lineas
            .Zip(pedido.Detalles, (e, d) => (e, d))
            .Count(par => par.e.CantidadEntregada != par.d.CantidadSolicitada);
}

// Alta o sustitución de un respaldo privado de la nota (spec SP8C
// "Documentos privados"): lo registra CAISY sobre un pedido despachado y antes
// de la recepción. El contenido pasa por el almacén privado, que valida firma,
// MIME, tamaño y dimensiones, y devuelve clave lógica y metadatos; SQL nunca
// conoce rutas ni contenido. El nombre de archivo se sanea para el campo
// nombre seguro y no llega al registro de vuelo (anti-PII).
public sealed record AgregarDocumentoNotaCommand(
    Guid PedidoId, Stream Contenido, string? NombreArchivo, Guid? ReemplazaDocumentoId)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.nota-documento", new Dictionary<string, DatoRegistroVuelo>
        { ["Sustituye"] = DatoRegistroVuelo.Entero });
}

public sealed class AgregarDocumentoNotaValidator : AbstractValidator<AgregarDocumentoNotaCommand>
{
    public AgregarDocumentoNotaValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.Contenido).NotNull();
        RuleFor(c => c.NombreArchivo).MaximumLength(260);
    }
}

public sealed class AgregarDocumentoNotaHandler(
    IRepositorioPedidosAlimento repositorio,
    IAlmacenDocumentosPedido almacen,
    OpcionesAlmacenDocumentosPedido opciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AgregarDocumentoNotaCommand, Guid>
{
    public async Task<Guid> Handle(AgregarDocumentoNotaCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Despachado)
            throw new ConflictException("Los respaldos de la nota se registran sobre un pedido despachado.");
        if (request.ReemplazaDocumentoId is { } previo
            && !pedido.Entrega!.Documentos.Any(d => d.Id == previo && d.Activo))
            throw new ConflictException("El documento a reemplazar no existe o ya fue reemplazado.");
        if (request.ReemplazaDocumentoId is null
            && pedido.Entrega!.Documentos.Count(d => d.Activo) >= opciones.MaxDocumentosPorNota)
            throw new ConflictException(
                $"La nota admite hasta {opciones.MaxDocumentosPorNota.ToString(CultureInfo.InvariantCulture)} imágenes.");

        var guardado = await almacen.GuardarAsync(request.Contenido, cancellationToken);
        var datos = new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, SanearNombre(request.NombreArchivo));
        var documento = request.ReemplazaDocumentoId is null
            ? pedido.AgregarDocumentoNota(datos)
            : pedido.ReemplazarDocumentoNota(request.ReemplazaDocumentoId.Value, datos);
        repositorio.AgregarDocumentoNota(documento);
        registroVuelo.Decidir("avicola.pedidos.nota-documento", "registro", "aplicada",
            new Dictionary<string, object?>
            {
                ["Sustituye"] = request.ReemplazaDocumentoId is null ? 0 : 1,
                ["Actor"] = actorId != Guid.Empty ? 1 : 0,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return documento.Id;
    }

    // Nombre seguro (spec SP8C): sin rutas ni caracteres problemáticos; si el
    // archivo llega sin nombre útil se usa uno genérico. No se guarda el nombre
    // original completo del cliente.
    private static string SanearNombre(string? nombreArchivo)
    {
        var nombre = Path.GetFileName(nombreArchivo?.Trim() ?? string.Empty);
        var caracteresInvalidos = Path.GetInvalidFileNameChars();
        var sano = new string(nombre
            .Select(c => Array.IndexOf(caracteresInvalidos, c) >= 0 ? '-' : c)
            .ToArray())
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', ' ');
        if (sano.Length > 200)
            sano = sano[^200..];
        return sano.Length == 0 ? "nota.jpg" : sano;
    }
}

// Descarga autorizada de un respaldo (spec SP8C): la vista derivada se sirve
// inline y el original solo como adjunto. El alcance lo impone el filtro del
// DbContext (el tenant solo encuentra los suyos; CAISY los ve todos) y un
// documento ajeno responde 404 genérico, sin revelar existencia.
public sealed record DescargarDocumentoNotaQuery(
    Guid PedidoId, Guid DocumentoId, bool EsOriginal) : IRequest<ArchivoDocumentoNota>;

public sealed record ArchivoDocumentoNota(
    Stream Contenido, string Mime, string NombreSeguro);

public sealed class DescargarDocumentoNotaHandler(
    IRepositorioPedidosAlimento repositorio,
    IAlmacenDocumentosPedido almacen)
    : IRequestHandler<DescargarDocumentoNotaQuery, ArchivoDocumentoNota>
{
    public async Task<ArchivoDocumentoNota> Handle(
        DescargarDocumentoNotaQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerConHistorialAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        var documento = pedido.Entrega?.Documentos.SingleOrDefault(d => d.Id == request.DocumentoId)
            ?? throw new NotFoundException("Documento de nota", request.DocumentoId);
        var contenido = request.EsOriginal
            ? await almacen.AbrirOriginalAsync(documento.ClaveOriginal, cancellationToken)
            : await almacen.AbrirVistaAsync(documento.ClaveVista, cancellationToken);
        if (contenido is null)
            throw new NotFoundException("Documento de nota", request.DocumentoId);
        return new ArchivoDocumentoNota(
            contenido,
            request.EsOriginal ? documento.Mime : "image/jpeg",
            documento.NombreSeguro);
    }
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

public sealed class ObtenerCupoPedidosHandler(
    IRepositorioPedidosAlimento repositorio,
    OpcionesPedidosAlimento opciones,
    ICurrentUser usuarioActual)
    : IRequestHandler<ObtenerCupoPedidosQuery, CupoPedidosResumen>
{
    public async Task<CupoPedidosResumen> Handle(
        ObtenerCupoPedidosQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("Solo una cuenta de tenant consulta el cupo.");
        var hoy = FechasNegocio.Hoy();
        var inicioSemana = SemanasIso.Inicio(hoy);
        var enviados = await repositorio.ContarEnviadosEnSemanaAsync(
            clienteId, inicioSemana, inicioSemana.AddDays(6), cancellationToken);
        return new CupoPedidosResumen(
            enviados, opciones.MaximoPorSemana, inicioSemana, inicioSemana.AddDays(6));
    }
}

// Bandeja global de CAISY (spec SP8): filtros por estado y presentación con
// paginación, ordenada por envío más reciente.
public sealed class ListarPedidosCaisyHandler(IRepositorioPedidosAlimento repositorio)
    : IRequestHandler<ListarPedidosCaisyQuery, PaginaPedidosCaisy>
{
    public async Task<PaginaPedidosCaisy> Handle(
        ListarPedidosCaisyQuery request, CancellationToken cancellationToken)
    {
        var estado = request.Estado is null
            ? (EstadoPedidoAlimento?)null : Enum.Parse<EstadoPedidoAlimento>(request.Estado, true);
        var presentacion = request.Presentacion is null
            ? (PresentacionAlimento?)null : Enum.Parse<PresentacionAlimento>(request.Presentacion, true);
        var saltar = (request.Pagina - 1) * request.TamanoPagina;
        var (items, total) = await repositorio.ListarPaginadoCaisyAsync(
            estado, presentacion, saltar, request.TamanoPagina, cancellationToken);
        return new PaginaPedidosCaisy(
            items.Select(MapeadorPedidos.MapearResumenCaisy).ToList(),
            total, request.Pagina, request.TamanoPagina);
    }
}

internal static class MapeadorPedidos
{
    public static PedidoAlimentoResumen MapearResumen(PedidoAlimento pedido) =>
        new(pedido.Id, pedido.Estado.ToString(), pedido.Detalles.First().Presentacion.ToString(),
            pedido.FechaPedido, pedido.FechaEntregaEstimada, pedido.TotalSolicitado,
            pedido.Detalles.Count);

    public static PedidoCaisyResumen MapearResumenCaisy(PedidoAlimento pedido) =>
        new(pedido.Id, pedido.ClienteId, pedido.Estado.ToString(),
            pedido.Detalles.First().Presentacion.ToString(),
            pedido.FechaPedido, pedido.FechaEntregaEstimada, pedido.TotalSolicitado,
            pedido.Detalles.Count);

    public static PedidoAlimentoDetalle MapearDetalle(PedidoAlimento pedido) =>
        new(pedido.Id, pedido.ClienteId, pedido.Estado.ToString(), pedido.FechaPedido,
            pedido.FechaEntregaEstimada, pedido.TotalSolicitado,
            pedido.Detalles
                .Select(d => new LineaPedidoAlimentoResumen(
                    d.Id, d.TipoAlimento.ToString(), d.Presentacion.ToString(),
                    d.CantidadSolicitada, d.Equivalentes40Kg,
                    d.PrecioFinalPor40Kg, d.SubtotalSolicitado,
                    d.NotificacionPreciosAlimentosId))
                .ToList(),
            pedido.Historial
                .Select(t => new TransicionPedidoAlimentoResumen(
                    t.EstadoOrigen.ToString(), t.EstadoDestino.ToString(), t.FechaUtc,
                    t.Motivo, t.FechaEntregaEstimada))
                .ToList());
}
