using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PreciosAlimentos;

public static class FechasNegocio
{
    // Fecha de negocio del sistema: Bolivia (America/La_Paz, spec SP8). El id
    // IANA funciona en Windows y Linux con .NET moderno.
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}

public sealed record ImportarNotificacionPdfCommand(Stream Contenido)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios.importar-pdf",
        new Dictionary<string, DatoRegistroVuelo> { ["DetallesImportados"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarBorradorPreciosCommand(
    Guid NotificacionId, DateOnly FechaDocumento, DateOnly VigenteDesde,
    decimal AporteCaisy, decimal Fondo, decimal Servicios,
    IReadOnlyList<DatosDetallePrecio> Detalles)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios.actualizar-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record PublicarNotificacionPreciosCommand(Guid NotificacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios.publicar",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record AnularNotificacionFuturaCommand(Guid NotificacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios.anular-futura",
        new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarNotificacionesPreciosQuery
    : IRequest<IReadOnlyList<NotificacionPreciosResumen>>;

public sealed record NotificacionPreciosResumen(
    Guid Id, DateOnly FechaDocumento, DateOnly VigenteDesde, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record ObtenerNotificacionPreciosQuery(Guid NotificacionId)
    : IRequest<NotificacionPreciosDetalle>;

public sealed record ObtenerPrecioVigenteQuery(DateOnly? Fecha)
    : IRequest<NotificacionPreciosDetalle?>;

public sealed record DetallePrecioResumen(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias);

public sealed record NotificacionPreciosDetalle(
    Guid Id, DateOnly FechaDocumento, DateOnly VigenteDesde, string Estado,
    decimal AporteCaisy, decimal Fondo, decimal Servicios, Guid? DocumentoOriginalId,
    IReadOnlyList<DetallePrecioResumen> Detalles);

public sealed record DescargarDocumentoOriginalQuery(Guid NotificacionId)
    : IRequest<Stream>;

public sealed class ActualizarBorradorPreciosValidator : AbstractValidator<ActualizarBorradorPreciosCommand>
{
    public ActualizarBorradorPreciosValidator()
    {
        RuleFor(c => c.AporteCaisy).GreaterThan(0);
        RuleFor(c => c.Fondo).GreaterThan(0);
        RuleFor(c => c.Servicios).GreaterThan(0);
        RuleFor(c => c.Detalles).NotNull().NotEmpty();
        RuleForEach(c => c.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(d => d.PrecioFinalPor40Kg).GreaterThan(0);
            detalle.RuleFor(d => d.EdadDesdeDias).GreaterThan(0)
                .When(d => d.EdadDesdeDias.HasValue);
            detalle.RuleFor(d => d.EdadHastaDias).GreaterThan(0)
                .When(d => d.EdadHastaDias.HasValue);
        });
    }
}

public sealed class PublicarNotificacionPreciosValidator : AbstractValidator<PublicarNotificacionPreciosCommand>
{
    public PublicarNotificacionPreciosValidator() =>
        RuleFor(c => c.NotificacionId).NotEmpty();
}

// El PDF original se guarda privado y el borrador se crea completo o no se
// crea (spec SP8): errores de formato o firma rechazan la importación entera.
public sealed class ImportarNotificacionPdfHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IImportadorNotificacionPreciosPdf importador,
    IAlmacenDocumentosPrecios almacen,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarNotificacionPdfCommand, Guid>
{
    public async Task<Guid> Handle(ImportarNotificacionPdfCommand request, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await request.Contenido.CopyToAsync(memoria, cancellationToken);
        var bytes = memoria.ToArray();
        if (!EsPdf(bytes))
            throw new ValidationException("El archivo no es un PDF válido.");

        var resultado = importador.Importar(new MemoryStream(bytes));
        if (resultado.Errores.Count > 0 || resultado.Propuesta is null)
            throw new ValidationException(resultado.Errores.Select(e =>
                new ValidationFailure("Documento",
                    e.Fila is { } fila ? $"Fila {fila}: {e.Mensaje}" : e.Mensaje)));

        var propuesta = resultado.Propuesta;
        Guid documentoOriginalId;
        await using (var original = new MemoryStream(bytes))
            documentoOriginalId = await almacen.GuardarAsync(original, cancellationToken);

        var notificacion = new NotificacionPreciosAlimentos(
            propuesta.FechaDocumento, propuesta.VigenteDesde,
            propuesta.AporteCaisy, propuesta.Fondo, propuesta.Servicios, propuesta.Detalles);
        notificacion.AsignarDocumentoOriginal(documentoOriginalId);
        repositorio.Agregar(notificacion);
        registroVuelo.Decidir("avicola.precios.importar-pdf", "importacion", "aplicada",
            new Dictionary<string, object?> { ["DetallesImportados"] = propuesta.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return notificacion.Id;
    }

    // Control de firma (spec Tarea 4: MIME/firma antes de tocar el importador).
    private static bool EsPdf(byte[] bytes) =>
        bytes.Length >= 5 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D' && bytes[3] == (byte)'F' && bytes[4] == (byte)'-';
}

public sealed class ActualizarBorradorPreciosHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ActualizarBorradorPreciosCommand>
{
    public async Task Handle(ActualizarBorradorPreciosCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        notificacion.ActualizarBorrador(
            request.FechaDocumento, request.VigenteDesde,
            request.AporteCaisy, request.Fondo, request.Servicios, request.Detalles);
        // Los detalles recreados llevan clave Guid generada en el dominio:
        // se registran como Added explícitamente (ver IRepositorioNotificacionesPrecios).
        foreach (var detalle in notificacion.Detalles)
            repositorio.AgregarDetalle(detalle);
        registroVuelo.Decidir("avicola.precios.actualizar-borrador", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = notificacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// La publicación exige confirmación explícita (spec SP8): unicidad de
// vigencia activa, control de la columna «Precio actual» contra la vigente y
// sellado inmediato del borrador. Nunca se publica automáticamente.
public sealed class PublicarNotificacionPreciosHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarNotificacionPreciosCommand>
{
    public async Task Handle(PublicarNotificacionPreciosCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        if (await repositorio.ExistePublicadaConVigenciaIgualAsync(
                notificacion.VigenteDesde, notificacion.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");

        var vigente = await repositorio.ObtenerVigenteAsync(notificacion.FechaDocumento, cancellationToken);
        var discrepancias = BuscarDiscrepancias(notificacion, vigente);
        if (discrepancias.Count > 0)
            throw new ValidationException(new[]
            {
                new ValidationFailure("Documento",
                    "El «Precio actual» del documento no coincide con la publicación vigente; revise el borrador antes de publicar."),
            });

        notificacion.Publicar();
        registroVuelo.Decidir("avicola.precios.publicar", "publicacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = notificacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    private static List<DetallePrecioAlimento> BuscarDiscrepancias(
        NotificacionPreciosAlimentos notificacion, NotificacionPreciosAlimentos? vigente)
    {
        if (vigente is null)
            return [];
        var preciosVigentes = vigente.Detalles.ToDictionary(
            d => (d.TipoAlimento, d.Presentacion), d => d.PrecioFinalPor40Kg);
        return notificacion.Detalles
            .Where(d => d.PrecioActualDocumento is { } precioActual
                && preciosVigentes.TryGetValue((d.TipoAlimento, d.Presentacion), out var precioVigente)
                && precioActual != precioVigente)
            .ToList();
    }
}

public sealed class AnularNotificacionFuturaHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AnularNotificacionFuturaCommand>
{
    public async Task Handle(AnularNotificacionFuturaCommand request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        // Solo una publicación futura se anula; una efectiva queda sellada
        // (spec SP8) y la corrección es otra publicación.
        notificacion.AnularFutura(FechasNegocio.Hoy());
        registroVuelo.Decidir("avicola.precios.anular-futura", "anulacion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListarNotificacionesPreciosHandler(IRepositorioNotificacionesPrecios repositorio)
    : IRequestHandler<ListarNotificacionesPreciosQuery, IReadOnlyList<NotificacionPreciosResumen>>
{
    public async Task<IReadOnlyList<NotificacionPreciosResumen>> Handle(
        ListarNotificacionesPreciosQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarHistorialAsync(cancellationToken))
            .Select(n => new NotificacionPreciosResumen(
                n.Id, n.FechaDocumento, n.VigenteDesde, n.Estado.ToString(),
                n.Detalles.Count, n.DocumentoOriginalId is not null))
            .ToList();
}

public sealed class ObtenerNotificacionPreciosHandler(IRepositorioNotificacionesPrecios repositorio)
    : IRequestHandler<ObtenerNotificacionPreciosQuery, NotificacionPreciosDetalle>
{
    public async Task<NotificacionPreciosDetalle> Handle(
        ObtenerNotificacionPreciosQuery request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        return MapeadorPrecios.Mapear(notificacion);
    }
}

// Fecha por defecto: hoy en Bolivia (spec SP8); la consulta se usa al preparar
// pedidos y al comparar contra el documento.
public sealed class ObtenerPrecioVigenteHandler(IRepositorioNotificacionesPrecios repositorio)
    : IRequestHandler<ObtenerPrecioVigenteQuery, NotificacionPreciosDetalle?>
{
    public async Task<NotificacionPreciosDetalle?> Handle(
        ObtenerPrecioVigenteQuery request, CancellationToken cancellationToken)
    {
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        return vigente is null ? null : MapeadorPrecios.Mapear(vigente);
    }
}

public sealed class DescargarDocumentoOriginalHandler(
    IRepositorioNotificacionesPrecios repositorio,
    IAlmacenDocumentosPrecios almacen)
    : IRequestHandler<DescargarDocumentoOriginalQuery, Stream>
{
    public async Task<Stream> Handle(
        DescargarDocumentoOriginalQuery request, CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerPorIdAsync(request.NotificacionId, cancellationToken)
            ?? throw new NotFoundException("Notificación de precios", request.NotificacionId);
        if (notificacion.DocumentoOriginalId is not { } clave)
            throw new NotFoundException("Documento original", request.NotificacionId);
        return await almacen.AbrirAsync(clave, cancellationToken)
            ?? throw new NotFoundException("Documento original", request.NotificacionId);
    }
}

internal static class MapeadorPrecios
{
    public static NotificacionPreciosDetalle Mapear(NotificacionPreciosAlimentos notificacion) =>
        new(notificacion.Id, notificacion.FechaDocumento, notificacion.VigenteDesde,
            notificacion.Estado.ToString(), notificacion.AporteCaisy, notificacion.Fondo,
            notificacion.Servicios, notificacion.DocumentoOriginalId,
            notificacion.Detalles
                .OrderBy(d => d.TipoAlimento).ThenBy(d => d.Presentacion)
                .Select(d => new DetallePrecioResumen(
                    d.Id, d.TipoAlimento.ToString(), d.Presentacion.ToString(),
                    d.PrecioFinalPor40Kg, d.PrecioActualDocumento, d.EdadDesdeDias, d.EdadHastaDias))
                .ToList());
}
