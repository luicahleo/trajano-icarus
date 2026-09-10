using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
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

public sealed record PrevisualizarCorreccionPrecioHuevoQuery(Guid PublicacionErroneaId, Guid PublicacionCorrectivaId)
    : IRequest<VistaPreviaCorreccionPrecioHuevo>;

public sealed record AjusteCorreccionPrecioHuevoResumen(Guid DespachoHuevoId, DateOnly? FechaRecepcion, decimal Monto);

public sealed record VistaPreviaCorreccionPrecioHuevo(
    IReadOnlyList<AjusteCorreccionPrecioHuevoResumen> Ajustes, decimal Total);

public sealed record CorregirPublicacionPrecioHuevoVigenteCommand(
    Guid PublicacionErroneaId, Guid PublicacionCorrectivaId, string Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.corregir-vigente",
        new Dictionary<string, DatoRegistroVuelo> { ["DespachosAjustados"] = DatoRegistroVuelo.Entero });
}

public sealed class CorregirPublicacionPrecioHuevoVigenteValidator
    : AbstractValidator<CorregirPublicacionPrecioHuevoVigenteCommand>
{
    public CorregirPublicacionPrecioHuevoVigenteValidator()
    {
        RuleFor(c => c.PublicacionErroneaId).NotEmpty();
        RuleFor(c => c.PublicacionCorrectivaId).NotEmpty();
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}

// Vista previa (spec SP9D): solo lectura, no publica ni corrige nada — el
// gestor la usa para decidir si confirma. Calcula la diferencia con los
// precios de la correctiva tal cual está (aunque todavía sea un borrador).
public sealed class PrevisualizarCorreccionPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IRepositorioDespachosHuevo repositorioDespachos)
    : IRequestHandler<PrevisualizarCorreccionPrecioHuevoQuery, VistaPreviaCorreccionPrecioHuevo>
{
    public async Task<VistaPreviaCorreccionPrecioHuevo> Handle(
        PrevisualizarCorreccionPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var erronea = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionErroneaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionErroneaId);
        var correctiva = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionCorrectivaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionCorrectivaId);

        var despachos = await repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, cancellationToken);
        var ajustes = despachos
            .Select(d => new AjusteCorreccionPrecioHuevoResumen(
                d.Id, d.FechaRecepcion, CalcularDiferencia(d, erronea, correctiva)))
            .Where(a => a.Monto != 0)
            .ToList();
        return new VistaPreviaCorreccionPrecioHuevo(ajustes, ajustes.Sum(a => a.Monto));
    }

    // Compartido con CorregirPublicacionPrecioHuevoVigenteHandler para que la
    // vista previa y la aplicación real calculen exactamente lo mismo.
    internal static decimal CalcularDiferencia(
        DespachoHuevo despacho, PublicacionPrecioHuevo erronea, PublicacionPrecioHuevo correctiva)
    {
        var preciosCorrectivos = correctiva.Detalles
            .ToDictionary(d => d.Tamano, d => d.PrecioAlProductor + correctiva.Servicio);
        var total = 0m;
        foreach (var linea in despacho.Detalles)
        {
            if (linea.PublicacionPrecioHuevoId != erronea.Id) continue;
            if (!preciosCorrectivos.TryGetValue(linea.Tamano, out var precioCorrecto)) continue;
            var cantidad = linea.CantidadAmarras * DetalleDespachoHuevo.HuevosPorAmarra + linea.UnidadesSueltas;
            total += (precioCorrecto - (linea.PrecioUnitarioCongelado ?? 0m)) * cantidad;
        }
        return total;
    }
}

// Corrección (spec SP9D): publica la correctiva, corrige la errónea y
// aplica un ajuste + notificación por cada despacho Recibido con diferencia
// distinta de cero. Solo se puede corregir la publicación que está
// realmente vigente hoy — no cualquier Publicada del historial — para no
// reconciliar despachos que ya estaban correctamente valorados a su propia
// fecha.
public sealed class CorregirPublicacionPrecioHuevoVigenteHandler(
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
    IRepositorioDespachosHuevo repositorioDespachos,
    IRepositorioAjustesCreditoHuevo repositorioAjustes,
    INotificacionesInternasDespachoHuevo notificaciones,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CorregirPublicacionPrecioHuevoVigenteCommand>
{
    public async Task Handle(CorregirPublicacionPrecioHuevoVigenteCommand request, CancellationToken cancellationToken)
    {
        var erronea = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionErroneaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionErroneaId);
        var correctiva = await repositorioPrecios.ObtenerPorIdAsync(request.PublicacionCorrectivaId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionCorrectivaId);
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        var vigenteActual = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken);
        if (vigenteActual is null || vigenteActual.Id != erronea.Id)
            throw new ConflictException("Solo se puede corregir la publicación vigente.");
        if (correctiva.FechaVigencia > hoy)
            throw new ValidationException("La publicación correctiva no puede tener vigencia futura.");
        if (await repositorioPrecios.ExistePublicadaConVigenciaIgualAsync(
                correctiva.FechaVigencia, correctiva.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");

        var despachos = await repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, cancellationToken);

        correctiva.Publicar();
        erronea.CorregirVigente(correctiva.Id, request.Motivo);

        var ajustados = 0;
        foreach (var despacho in despachos)
        {
            var monto = PrevisualizarCorreccionPrecioHuevoHandler.CalcularDiferencia(despacho, erronea, correctiva);
            if (monto == 0) continue;
            var ajuste = new AjusteCreditoHuevo(
                despacho.ClienteId, despacho.Id, erronea.Id, correctiva.Id, monto, request.Motivo, actorId);
            repositorioAjustes.Agregar(ajuste);
            notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaAjusteCredito(
                despacho.Id, despacho.ClienteId,
                string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{monto:0.00} Bs — {request.Motivo}")));
            ajustados++;
        }

        registroVuelo.Decidir("avicola.precios-huevo.corregir-vigente", "correccion", "aplicada",
            new Dictionary<string, object?> { ["DespachosAjustados"] = ajustados });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
