using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9B Task 3 (spec SP9): el borrador se crea solo con granja activa del
// tenant; el envío congela el precio al productor vigente y exige la foto de
// la nota en la misma operación — si falta la publicación vigente se rechaza
// antes de guardar el documento, sin transición a medias.
public class DespachosHuevoHandlerTests
{
    private static readonly DateOnly FechaNotificacion = new(2025, 11, 2);
    private static readonly DateOnly FechaVigencia = new(2025, 11, 10);

    private readonly IRepositorioDespachosHuevo _repositorio = Substitute.For<IRepositorioDespachosHuevo>();
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IRepositorioPublicacionesPreciosHuevo _repositorioPrecios =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
    private readonly IAlmacenDocumentosPedido _almacen = Substitute.For<IAlmacenDocumentosPedido>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    private CrearBorradorDespachoHuevoHandler CrearCreador() =>
        new(_repositorio, _granjas, _usuarioActual, _registroVuelo, _unidadTrabajo);

    private DespacharDespachoHuevoHandler CrearDespachador() =>
        new(_repositorio, _repositorioPrecios, _almacen, _usuarioActual, _registroVuelo, _unidadTrabajo);

    private static IReadOnlyList<LineaDespachoHuevo> UnaLinea() =>
        [new("Primera", 2, 30)];

    private static DespachoHuevo CrearBorrador() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 2, 30)]);

    private static PublicacionPrecioHuevo PublicacionVigente() =>
        new(FechaNotificacion, FechaVigencia, 0.40m,
            [new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 12.50m)]);

    private static DocumentoAlmacenado DocumentoGuardado() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "hash-sha256", "image/jpeg", 1024, 512);

    [Fact]
    public async Task CrearBorradorSinGranjaActivaLanzaValidationException()
    {
        _usuarioActual.ClienteId.Returns(Guid.NewGuid());
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>())
            .Returns((Granja?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CrearCreador().Handle(
                new CrearBorradorDespachoHuevoCommand(UnaLinea()), CancellationToken.None));

        _repositorio.DidNotReceive().Agregar(Arg.Any<DespachoHuevo>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DespacharConEstadoNoBorradorLanzaConflict()
    {
        var despacho = CrearBorrador();
        despacho.Despachar(
            FechaVigencia, Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Primera, 12.50m, Guid.NewGuid())],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "nota.jpg"));
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearDespachador().Handle(
                new DespacharDespachoHuevoCommand(
                    despacho.Id, new MemoryStream("foto"u8.ToArray()), "nota.jpg"),
                CancellationToken.None));

        Assert.Equal("Solo un despacho en borrador se puede enviar.", excepcion.Message);
        await _almacen.DidNotReceive().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DespacharSinPublicacionVigenteLanzaValidationYNoGuardaDocumento()
    {
        var despacho = CrearBorrador();
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublicacionPrecioHuevo?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CrearDespachador().Handle(
                new DespacharDespachoHuevoCommand(
                    despacho.Id, new MemoryStream("foto"u8.ToArray()), "nota.jpg"),
                CancellationToken.None));

        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        await _almacen.DidNotReceive().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DespacharCongelaPrecioVigenteYEnvia()
    {
        var despacho = CrearBorrador();
        var publicacion = PublicacionVigente();
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(publicacion);
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(DocumentoGuardado());

        await CrearDespachador().Handle(
            new DespacharDespachoHuevoCommand(
                despacho.Id, new MemoryStream("foto"u8.ToArray()), "nota.jpg"),
            CancellationToken.None);

        Assert.Equal(EstadoDespachoHuevo.Despachado, despacho.Estado);
        Assert.NotNull(despacho.FechaDespacho);
        var linea = Assert.Single(despacho.Detalles);
        Assert.Equal(12.50m, linea.PrecioProductorCongelado);
        Assert.Equal(publicacion.Id, linea.PublicacionPrecioHuevoId);
        Assert.Equal(390 * 12.50m, linea.Subtotal);
        Assert.NotNull(despacho.DocumentoNota);
        await _almacen.Received(1).GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerConIdInexistenteEs404()
    {
        _repositorio.ObtenerConHistorialAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DespachoHuevo?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ObtenerDespachoHuevoHandler(_repositorio).Handle(
                new ObtenerDespachoHuevoQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
