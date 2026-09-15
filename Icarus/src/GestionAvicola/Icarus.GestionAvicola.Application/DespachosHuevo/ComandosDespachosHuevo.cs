using System.Globalization;
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

// Filtros del listado del tenant (spec 2026-09-14): sin presentación, que no
// existe en el despacho de huevo. CreadoPorTrabajadorId no viaja a CAISY.
public sealed record FiltrosDespachosTenant(
    Guid? GranjaId, string? Estado, DateOnly? Desde, DateOnly? Hasta,
    Guid? CreadoPorTrabajadorId, int? Numero);

public sealed record ListarDespachosHuevoTenantQuery(
    FiltrosDespachosTenant Filtros, PeticionPaginada Paginacion)
    : IRequest<Pagina<DespachoHuevoResumen>>;

public sealed record DespachoHuevoResumen(
    Guid Id, string Folio, int Numero, Guid GranjaId, Guid? CreadoPorTrabajadorId,
    string Estado, DateOnly? FechaDespacho, int TotalAmarras, int TotalHuevos, decimal? TotalBs);

public sealed record ObtenerDespachoHuevoQuery(Guid DespachoId) : IRequest<DespachoHuevoDetalle>;

public sealed record DetalleDespachoHuevoResumen(
    Guid Id, string Tamano, int CantidadAmarras, int UnidadesSueltas, int CantidadHuevos,
    decimal? PrecioUnitarioCongelado, decimal? Subtotal);

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

        var despacho = new DespachoHuevo(
            clienteId, granja.Id, actorId, usuarioActual.TrabajadorId, ParsearLineas(request.Lineas));
        repositorio.Agregar(despacho);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.despachos-huevo.crear-borrador",
                ("CantidadLineas", DatoRegistroVuelo.Entero)),
            "creacion", "aplicada",
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
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.despachos-huevo.editar-borrador",
                ("CantidadLineas", DatoRegistroVuelo.Entero)),
            "edicion", "aplicada",
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
        registroVuelo.Decidir("avicola.despachos-huevo.desactivar-borrador", "borrado", "aplicada");
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
        // El despacho se valora al precio unitario (productor + servicio), el
        // monto que CAISY reconoce por huevo (glosario: recibo y crédito).
        var precios = vigente.Detalles
            .Select(d => new DatosPrecioDespachoHuevo(
                d.Tamano, d.PrecioAlProductor + vigente.Servicio, vigente.Id))
            .ToList();

        var guardado = await almacen.GuardarAsync(request.Contenido, cancellationToken);
        var documento = new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, SanearNombre(request.NombreArchivo));

        despacho.Despachar(hoy, actorId, precios, documento);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.despachos-huevo.despachar",
                ("Lineas", DatoRegistroVuelo.Entero),
                ("PublicacionPrecioHuevoId", DatoRegistroVuelo.Identificador)),
            "envio", "aplicada",
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

public sealed class ListarDespachosHuevoTenantValidator
    : AbstractValidator<ListarDespachosHuevoTenantQuery>
{
    public ListarDespachosHuevoTenantValidator()
    {
        RuleFor(c => c.Filtros.Estado)
            .Must(e => e is null || Enum.TryParse<EstadoDespachoHuevo>(e, true, out _))
            .WithMessage("El estado indicado no existe.");
    }
}

public sealed class ListarDespachosHuevoTenantHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoTenantQuery, Pagina<DespachoHuevoResumen>>
{
    public async Task<Pagina<DespachoHuevoResumen>> Handle(
        ListarDespachosHuevoTenantQuery request, CancellationToken cancellationToken)
    {
        var filtros = request.Filtros;
        var estado = filtros.Estado is null
            ? (EstadoDespachoHuevo?)null : Enum.Parse<EstadoDespachoHuevo>(filtros.Estado, true);
        var (items, total) = await repositorio.ListarPaginadoTenantAsync(
            filtros.GranjaId, estado, filtros.Desde, filtros.Hasta,
            filtros.CreadoPorTrabajadorId, filtros.Numero,
            request.Paginacion.Salto, request.Paginacion.TamanoNormalizado, cancellationToken);
        return new Pagina<DespachoHuevoResumen>(
            items.Select(MapeadorDespachos.MapearResumen).ToList(),
            total, request.Paginacion.PaginaNormalizada, request.Paginacion.TamanoNormalizado);
    }
}

// Bandeja global de CAISY (spec SP9C): filtro por estado con paginación,
// igual que pedidos de alimento. El resumen de CAISY NO lleva autor ni granja:
// CAISY ve el folio, nunca personas (decisión central de la spec 2026-09-14).
public sealed record ListarDespachosHuevoCaisyQuery(
    EstadoDespachoHuevo? Estado, string? Granja, DateOnly? Desde, DateOnly? Hasta, int? Numero,
    int Pagina, int TamanoPagina)
    : IRequest<PaginaDespachosHuevo>;

public sealed record DespachoHuevoCaisyResumen(
    Guid Id, string Folio, int Numero, string? GranjaNombre, string Estado, DateOnly? FechaDespacho,
    int TotalAmarras, int TotalHuevos, decimal? TotalBs);

public sealed record PaginaDespachosHuevo(IReadOnlyList<DespachoHuevoCaisyResumen> Items, int Total);

// Detalle para la bandeja global de CAISY (spec 2026-09-14). Existe aparte de
// ObtenerDespachoHuevoQuery porque el tenant sí tiene que ver sus borradores
// y las dos rutas compartían el mismo query.
public sealed record ObtenerDespachoHuevoCaisyQuery(Guid DespachoId)
    : IRequest<DespachoHuevoDetalle>;

public sealed class ListarDespachosHuevoCaisyHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ListarDespachosHuevoCaisyQuery, PaginaDespachosHuevo>
{
    public async Task<PaginaDespachosHuevo> Handle(
        ListarDespachosHuevoCaisyQuery request, CancellationToken cancellationToken)
    {
        var saltar = (Math.Max(request.Pagina, 1) - 1) * Math.Max(request.TamanoPagina, 1);
        var (items, total, granjas) = await repositorio.ListarPaginadoCaisyAsync(
            request.Estado, request.Granja, request.Desde, request.Hasta, request.Numero,
            saltar, Math.Max(request.TamanoPagina, 1), cancellationToken);
        return new PaginaDespachosHuevo(
            items.Select(d => MapeadorDespachos.MapearResumenCaisy(d, granjas)).ToList(),
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
                    d.CantidadHuevos, d.PrecioUnitarioCongelado, d.Subtotal))
                .ToList());
    }
}

public sealed class ObtenerDespachoHuevoCaisyHandler(IRepositorioDespachosHuevo repositorio)
    : IRequestHandler<ObtenerDespachoHuevoCaisyQuery, DespachoHuevoDetalle>
{
    public async Task<DespachoHuevoDetalle> Handle(
        ObtenerDespachoHuevoCaisyQuery request, CancellationToken cancellationToken)
    {
        var despacho = await repositorio.ObtenerConHistorialAsync(
            request.DespachoId, cancellationToken);
        // Un borrador se trata como inexistente: el 404 no revela que está ahí.
        if (despacho is null || despacho.Estado == EstadoDespachoHuevo.Borrador)
            throw new NotFoundException("Despacho de huevo", request.DespachoId);
        return new DespachoHuevoDetalle(
            despacho.Id, despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs,
            despacho.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetalleDespachoHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.CantidadAmarras, d.UnidadesSueltas,
                    d.CantidadHuevos, d.PrecioUnitarioCongelado, d.Subtotal))
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
    IRepositorioPublicacionesPreciosHuevo repositorioPrecios,
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

        await ReconciliarPreciosCorregidosAsync(despacho, cancellationToken);

        despacho.ConfirmarRecepcion(FechasNegocio.Hoy(), actorId);
        notificaciones.Agregar(NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            despacho.Id, despacho.ClienteId));
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.despachos-huevo.confirmar-recepcion",
                ("TotalBs", DatoRegistroVuelo.Decimal)),
            "recepcion", "aplicada",
            new Dictionary<string, object?> { ["TotalBs"] = despacho.TotalBs });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // Reconciliación perezosa (spec SP9D): si la publicación que congeló una
    // línea fue corregida mientras el despacho seguía en tránsito, la línea
    // se recongela contra la publicación activa al final de la cadena de
    // correcciones, antes de sellar la recepción. Un despacho ya Recibido
    // nunca pasa por acá — su camino de corrección es AjusteCreditoHuevo. La
    // publicación final debe estar Publicada: si la cadena termina en un
    // Borrador o Anulada, recongelar copiaría un precio que todavía no tiene
    // efecto (o que ya no existe), así que la línea conserva su congelado.
    private async Task ReconciliarPreciosCorregidosAsync(DespachoHuevo despacho, CancellationToken cancellationToken)
    {
        foreach (var linea in despacho.Detalles)
        {
            if (linea.PublicacionPrecioHuevoId is not { } publicacionId)
                continue;
            var idOriginal = publicacionId;
            var publicacion = await repositorioPrecios.ObtenerPorIdAsync(publicacionId, cancellationToken);
            while (publicacion is not null
                && publicacion.Estado == EstadoPublicacionPrecioHuevo.Corregida
                && publicacion.PublicacionCorrectivaId is { } siguienteId)
            {
                publicacion = await repositorioPrecios.ObtenerPorIdAsync(siguienteId, cancellationToken);
            }
            if (publicacion is null || publicacion.Id == idOriginal
                || publicacion.Estado != EstadoPublicacionPrecioHuevo.Publicada)
                continue;
            var precio = publicacion.Detalles.SingleOrDefault(d => d.Tamano == linea.Tamano);
            if (precio is null)
                continue;
            despacho.RecongelarLinea(linea.Tamano, precio.PrecioAlProductor + publicacion.Servicio, publicacion.Id);
        }
    }
}

internal static class MapeadorDespachos
{
    // D-000045. El prefijo es presentación: no se persiste.
    public static string FolioDe(int numero) =>
        string.Create(CultureInfo.InvariantCulture, $"D-{numero:D6}");

    public static DespachoHuevoResumen MapearResumen(DespachoHuevo despacho) =>
        new(despacho.Id, FolioDe(despacho.Numero), despacho.Numero, despacho.GranjaId,
            despacho.CreadoPorTrabajadorId, despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs);

    public static DespachoHuevoCaisyResumen MapearResumenCaisy(
        DespachoHuevo despacho, IReadOnlyDictionary<Guid, string> granjas) =>
        new(despacho.Id, FolioDe(despacho.Numero), despacho.Numero,
            granjas.TryGetValue(despacho.GranjaId, out var nombre) ? nombre : null,
            despacho.Estado.ToString(), despacho.FechaDespacho,
            despacho.TotalAmarras, despacho.TotalHuevos, despacho.TotalBs);
}
