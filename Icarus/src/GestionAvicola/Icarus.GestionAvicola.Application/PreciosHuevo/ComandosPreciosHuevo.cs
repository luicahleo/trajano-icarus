using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PreciosHuevo;

// Fecha de negocio del sistema: Bolivia (America/La_Paz), misma regla que
// FechasNegocio en Icarus.GestionAvicola.Application.PreciosAlimentos. Se
// duplica en este archivo (en vez de referenciar la otra carpeta de feature)
// para mantener PreciosHuevo autocontenido, siguiendo el mismo aislamiento
// por carpeta que ya separa PreciosAlimentos, PedidosAlimento y Vacunacion.
public static class FechasNegocio
{
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}

public sealed record ImportarPublicacionPrecioHuevoExcelCommand(Stream Contenido)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.importar-excel",
        new Dictionary<string, DatoRegistroVuelo> { ["DetallesImportados"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarBorradorPrecioHuevoCommand(
    Guid PublicacionId, DateOnly FechaNotificacion, DateOnly FechaVigencia,
    decimal Servicio, IReadOnlyList<DatosDetallePrecioHuevo> Detalles)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.actualizar-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record PublicarPublicacionPrecioHuevoCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.publicar",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record AnularPublicacionPrecioHuevoFuturaCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.anular-futura", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DescartarBorradorPrecioHuevoCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.descartar-borrador", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarPublicacionesPrecioHuevoQuery
    : IRequest<IReadOnlyList<PublicacionPrecioHuevoResumen>>;

public sealed record PublicacionPrecioHuevoResumen(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record ObtenerPublicacionPrecioHuevoQuery(Guid PublicacionId)
    : IRequest<PublicacionPrecioHuevoDetalle>;

public sealed record ObtenerPrecioHuevoVigenteQuery(DateOnly? Fecha)
    : IRequest<PublicacionPrecioHuevoDetalle?>;

public sealed record DetallePrecioHuevoResumen(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario);

public sealed record PublicacionPrecioHuevoDetalle(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    decimal Servicio, Guid? DocumentoOriginalId, IReadOnlyList<DetallePrecioHuevoResumen> Detalles);

public sealed record DescargarDocumentoOriginalPrecioHuevoQuery(Guid PublicacionId)
    : IRequest<Stream>;

public sealed class ActualizarBorradorPrecioHuevoValidator : AbstractValidator<ActualizarBorradorPrecioHuevoCommand>
{
    public ActualizarBorradorPrecioHuevoValidator()
    {
        RuleFor(c => c.Servicio).GreaterThan(0);
        RuleFor(c => c.Detalles).NotNull().NotEmpty();
        RuleForEach(c => c.Detalles).ChildRules(detalle =>
            detalle.RuleFor(d => d.PrecioAlProductor).GreaterThan(0));
    }
}

public sealed class PublicarPublicacionPrecioHuevoValidator : AbstractValidator<PublicarPublicacionPrecioHuevoCommand>
{
    public PublicarPublicacionPrecioHuevoValidator() => RuleFor(c => c.PublicacionId).NotEmpty();
}

public sealed class ImportarPublicacionPrecioHuevoExcelHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IImportadorPublicacionPrecioHuevoExcel importador,
    IAlmacenDocumentosPrecios almacen,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarPublicacionPrecioHuevoExcelCommand, Guid>
{
    public async Task<Guid> Handle(ImportarPublicacionPrecioHuevoExcelCommand request, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await request.Contenido.CopyToAsync(memoria, cancellationToken);
        var bytes = memoria.ToArray();
        var resultado = importador.Importar(new MemoryStream(bytes));
        if (resultado.Errores.Count > 0 || resultado.Propuesta is null)
            throw new ValidationException(resultado.Errores.Select(e => new ValidationFailure(
                "Documento", e.Fila is { } fila ? $"Fila {fila}: {e.Mensaje}" : e.Mensaje)));
        Guid documentoOriginalId;
        await using (var original = new MemoryStream(bytes))
            documentoOriginalId = await almacen.GuardarAsync(original, cancellationToken);
        var propuesta = resultado.Propuesta;
        var publicacion = new PublicacionPrecioHuevo(
            propuesta.FechaNotificacion, propuesta.FechaVigencia, propuesta.Servicio, propuesta.Detalles);
        publicacion.AsignarDocumentoOriginal(documentoOriginalId);
        repositorio.Agregar(publicacion);
        registroVuelo.Decidir("avicola.precios-huevo.importar-excel", "importacion", "aplicada",
            new Dictionary<string, object?> { ["DetallesImportados"] = propuesta.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return publicacion.Id;
    }
}

public sealed class ActualizarBorradorPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ActualizarBorradorPrecioHuevoCommand>
{
    public async Task Handle(ActualizarBorradorPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.ActualizarBorrador(
            request.FechaNotificacion, request.FechaVigencia, request.Servicio, request.Detalles);
        foreach (var detalle in publicacion.Detalles)
            repositorio.AgregarDetalle(detalle);
        registroVuelo.Decidir("avicola.precios-huevo.actualizar-borrador", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = publicacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// A diferencia de alimento, publicar precio de huevo no bloquea por
// discrepancia con la vigente: GestorRecepcionHuevos es la única fuente y el
// control «Precio Actual» queda solo como referencia visual (spec SP9).
public sealed class PublicarPublicacionPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarPublicacionPrecioHuevoCommand>
{
    public async Task Handle(PublicarPublicacionPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        if (await repositorio.ExistePublicadaConVigenciaIgualAsync(
                publicacion.FechaVigencia, publicacion.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");
        publicacion.Publicar();
        registroVuelo.Decidir("avicola.precios-huevo.publicar", "publicacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = publicacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AnularPublicacionPrecioHuevoFuturaHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AnularPublicacionPrecioHuevoFuturaCommand>
{
    public async Task Handle(AnularPublicacionPrecioHuevoFuturaCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.AnularFutura(FechasNegocio.Hoy());
        registroVuelo.Decidir("avicola.precios-huevo.anular-futura", "anulacion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DescartarBorradorPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DescartarBorradorPrecioHuevoCommand>
{
    public async Task Handle(DescartarBorradorPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.DescartarBorrador();
        registroVuelo.Decidir("avicola.precios-huevo.descartar-borrador", "borrado", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListarPublicacionesPrecioHuevoHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ListarPublicacionesPrecioHuevoQuery, IReadOnlyList<PublicacionPrecioHuevoResumen>>
{
    public async Task<IReadOnlyList<PublicacionPrecioHuevoResumen>> Handle(
        ListarPublicacionesPrecioHuevoQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarHistorialAsync(cancellationToken))
            .Select(p => new PublicacionPrecioHuevoResumen(
                p.Id, p.FechaNotificacion, p.FechaVigencia, p.Estado.ToString(),
                p.Detalles.Count, p.DocumentoOriginalId is not null))
            .ToList();
}

public sealed class ObtenerPublicacionPrecioHuevoHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ObtenerPublicacionPrecioHuevoQuery, PublicacionPrecioHuevoDetalle>
{
    public async Task<PublicacionPrecioHuevoDetalle> Handle(
        ObtenerPublicacionPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        return MapeadorPreciosHuevo.Mapear(publicacion);
    }
}

public sealed class ObtenerPrecioHuevoVigenteHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ObtenerPrecioHuevoVigenteQuery, PublicacionPrecioHuevoDetalle?>
{
    public async Task<PublicacionPrecioHuevoDetalle?> Handle(
        ObtenerPrecioHuevoVigenteQuery request, CancellationToken cancellationToken)
    {
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        return vigente is null ? null : MapeadorPreciosHuevo.Mapear(vigente);
    }
}

public sealed class DescargarDocumentoOriginalPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IAlmacenDocumentosPrecios almacen)
    : IRequestHandler<DescargarDocumentoOriginalPrecioHuevoQuery, Stream>
{
    public async Task<Stream> Handle(
        DescargarDocumentoOriginalPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        if (publicacion.DocumentoOriginalId is not { } clave)
            throw new NotFoundException("Documento original", request.PublicacionId);
        return await almacen.AbrirAsync(clave, cancellationToken)
            ?? throw new NotFoundException("Documento original", request.PublicacionId);
    }
}

internal static class MapeadorPreciosHuevo
{
    public static PublicacionPrecioHuevoDetalle Mapear(PublicacionPrecioHuevo publicacion) =>
        new(publicacion.Id, publicacion.FechaNotificacion, publicacion.FechaVigencia,
            publicacion.Estado.ToString(), publicacion.Servicio, publicacion.DocumentoOriginalId,
            publicacion.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetallePrecioHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.PrecioAlProductor, d.PrecioActualDocumento,
                    d.PrecioAlProductor + publicacion.Servicio))
                .ToList());
}
