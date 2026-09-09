using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Fecha de negocio Bolivia (spec SP9); ver la nota de duplicación en
// ComandosPreciosHuevo.cs — cada carpeta de feature se mantiene autocontenida.
public static class FechasNegocio
{
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}

public sealed record LineaDespachoHuevo(string Tamano, int CantidadAmarras, int UnidadesSueltas);

public sealed record CrearBorradorDespachoHuevoCommand(IReadOnlyList<LineaDespachoHuevo> Lineas)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.crear-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadLineas"] = DatoRegistroVuelo.Entero });
}

public sealed record EditarBorradorDespachoHuevoCommand(
    Guid DespachoId, IReadOnlyList<LineaDespachoHuevo> Lineas)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.editar-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadLineas"] = DatoRegistroVuelo.Entero });
}

public sealed record DesactivarBorradorDespachoHuevoCommand(Guid DespachoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.desactivar-borrador", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DespacharDespachoHuevoCommand(
    Guid DespachoId, Stream Contenido, string NombreArchivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.despachar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarDespachosHuevoTenantQuery : IRequest<IReadOnlyList<DespachoHuevoResumen>>;

public sealed record DespachoHuevoResumen(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos, decimal? TotalBs);

public sealed record ObtenerDespachoHuevoQuery(Guid DespachoId) : IRequest<DespachoHuevoDetalle>;

public sealed record DetalleDespachoHuevoResumen(
    Guid Id, string Tamano, int CantidadAmarras, int UnidadesSueltas, int CantidadHuevos,
    decimal? PrecioProductorCongelado, decimal? Subtotal);

public sealed record DespachoHuevoDetalle(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos,
    decimal? TotalBs, IReadOnlyList<DetalleDespachoHuevoResumen> Detalles);

public sealed class CrearBorradorDespachoHuevoValidator : AbstractValidator<CrearBorradorDespachoHuevoCommand>
{
    public CrearBorradorDespachoHuevoValidator() => RuleFor(c => c.Lineas).NotNull().NotEmpty();
}

public sealed class CrearBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRepositorioGranjas granjas,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CrearBorradorDespachoHuevoCommand, Guid>
{
    public async Task<Guid> Handle(CrearBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var granja = await granjas.ObtenerActivaDelTenantAsync(cancellationToken)
            ?? throw new ValidationException("El cliente debe tener una granja activa registrada.");

        var despacho = new DespachoHuevo(clienteId, granja.Id, actorId, ParsearLineas(request.Lineas));
        repositorio.Agregar(despacho);
        registroVuelo.Decidir("avicola.despachos-huevo.crear-borrador", "creacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadLineas"] = despacho.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return despacho.Id;
    }

    internal static IReadOnlyList<DatosDetalleDespachoHuevo> ParsearLineas(
        IReadOnlyList<LineaDespachoHuevo> lineas) =>
        lineas.Select(l =>
        {
            if (!Enum.TryParse<TamanoHuevo>(l.Tamano, true, out var tamano))
                throw new ValidationException("El tamaño de huevo indicado no existe.");
            return new DatosDetalleDespachoHuevo(tamano, l.CantidadAmarras, l.UnidadesSueltas);
        }).ToList();
}

public sealed class EditarBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<EditarBorradorDespachoHuevoCommand>
{
    public async Task Handle(EditarBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        despacho.EditarDetalles(CrearBorradorDespachoHuevoHandler.ParsearLineas(request.Lineas));
        foreach (var detalle in despacho.Detalles)
            repositorio.AgregarDetalle(detalle);
        registroVuelo.Decidir("avicola.despachos-huevo.editar-borrador", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadLineas"] = despacho.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DesactivarBorradorDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarBorradorDespachoHuevoCommand>
{
    public async Task Handle(DesactivarBorradorDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Borrador)
            throw new ConflictException("Solo un borrador se puede desactivar.");
        despacho.Desactivar();
        registroVuelo.Decidir("avicola.despachos-huevo.desactivar-borrador", "borrado", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Despachar (spec SP9): congela el precio al productor vigente por tamaño y
// exige la foto de la nota en la misma operación. Si falla el guardado del
// archivo, la excepción propaga antes de tocar el agregado (mismo orden que
// ConfirmarRecepcionPedidoHandler en SP8C/D): no queda transición a medias.
public sealed class DespacharDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IAlmacenDocumentosPedido almacen,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DespacharDespachoHuevoCommand>
{
    public async Task Handle(DespacharDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Borrador)
            throw new ConflictException("Solo un despacho en borrador se puede enviar.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        var vigente = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken)
            ?? throw new ValidationException("No hay una publicación de precios de huevo vigente.");
        var precios = vigente.Detalles
            .Select(d => new DatosPrecioDespachoHuevo(d.Tamano, d.PrecioAlProductor, vigente.Id))
            .ToList();

        var guardado = await almacen.GuardarAsync(request.Contenido, cancellationToken);
        var documento = new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, SanearNombre(request.NombreArchivo));

        despacho.Despachar(hoy, actorId, precios, documento);
        registroVuelo.Decidir("avicola.despachos-huevo.despachar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = despacho.Detalles.Count,
                ["PublicacionPrecioHuevoId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    private static string SanearNombre(string? nombreArchivo)
    {
        var nombre = Path.GetFileName(nombreArchivo?.Trim() ?? string.Empty);
        var sano = new string(nombre
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '-')
            .ToArray())
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', ' ');
        if (sano.Length > 200) sano = sano[^200..];
        return sano.Length == 0 ? "nota-despacho.jpg" : sano;
    }
}

public sealed class ListarDespachosHuevoTenantHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoTenantQuery, IReadOnlyList<DespachoHuevoResumen>>
{
    public async Task<IReadOnlyList<DespachoHuevoResumen>> Handle(
        ListarDespachosHuevoTenantQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarDelTenantAsync(cancellationToken))
            .OrderByDescending(d => d.FechaDespacho)
            .ThenByDescending(d => d.Id)
            .Select(d => new DespachoHuevoResumen(
                d.Id, d.Estado.ToString(), d.FechaDespacho, d.TotalAmarras, d.TotalHuevos, d.TotalBs))
            .ToList();
}

// Bandeja global de CAISY (spec SP9C): filtro por estado con paginación,
// igual que pedidos de alimento. Reutiliza el resumen del tenant: el shape
// es idéntico y la bandeja es global (sin filtro de tenant en el handler).
public sealed record ListarDespachosHuevoCaisyQuery(EstadoDespachoHuevo? Estado, int Pagina, int TamanoPagina)
    : IRequest<PaginaDespachosHuevo>;

public sealed record PaginaDespachosHuevo(IReadOnlyList<DespachoHuevoResumen> Items, int Total);

public sealed class ListarDespachosHuevoCaisyHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoCaisyQuery, PaginaDespachosHuevo>
{
    public async Task<PaginaDespachosHuevo> Handle(
        ListarDespachosHuevoCaisyQuery request, CancellationToken cancellationToken)
    {
        var saltar = (Math.Max(request.Pagina, 1) - 1) * Math.Max(request.TamanoPagina, 1);
        var (items, total) = await repositorio.ListarPaginadoCaisyAsync(
            request.Estado, saltar, Math.Max(request.TamanoPagina, 1), cancellationToken);
        return new PaginaDespachosHuevo(
            items.Select(d => new DespachoHuevoResumen(
                d.Id, d.Estado.ToString(), d.FechaDespacho, d.TotalAmarras, d.TotalHuevos, d.TotalBs)).ToList(),
            total);
    }
}

public sealed class ObtenerDespachoHuevoHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ObtenerDespachoHuevoQuery, DespachoHuevoDetalle>
{
    public async Task<DespachoHuevoDetalle> Handle(
        ObtenerDespachoHuevoQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        return new DespachoHuevoDetalle(
            despacho.Id, despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs,
            despacho.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetalleDespachoHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.CantidadAmarras, d.UnidadesSueltas,
                    d.CantidadHuevos, d.PrecioProductorCongelado, d.Subtotal))
                .ToList());
    }
}

// Recepción (spec SP9C): la confirma CAISY sobre un despacho despachado. No
// hay reconteo por línea: la operación solo cierra el estado, fija la fecha
// de recepción (fecha de negocio del servidor) y notifica a la bandeja del
// tenant. Los reintentos chocan con el estado y responden 409 sin duplicar
// nada.
public sealed record ConfirmarRecepcionDespachoHuevoCommand(Guid DespachoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.despachos-huevo.confirmar-recepcion", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed class ConfirmarRecepcionDespachoHuevoHandler(
    IRepositorioDespachosHuevo repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternasDespachoHuevo notificaciones)
    : IRequestHandler<ConfirmarRecepcionDespachoHuevoCommand>
{
    public async Task Handle(ConfirmarRecepcionDespachoHuevoCommand request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerPorIdAsync(request.DespachoId, cancellationToken)
            ?? throw new NotFoundException("Despacho de huevo", request.DespachoId);
        if (despacho.Estado != EstadoDespachoHuevo.Despachado)
            throw new ConflictException("Solo un despacho despachado se puede recibir.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        despacho.ConfirmarRecepcion(FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            despacho.Id, despacho.ClienteId));
        registroVuelo.Decidir("avicola.despachos-huevo.confirmar-recepcion", "recepcion", "aplicada",
            new Dictionary<string, object?> { ["TotalBs"] = despacho.TotalBs });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
