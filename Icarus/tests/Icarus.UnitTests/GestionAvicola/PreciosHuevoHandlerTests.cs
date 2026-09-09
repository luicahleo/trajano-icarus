using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9A Tarea 4 (spec SP9): la importación del Excel crea el borrador completo
// o rechaza la operación sin persistencia parcial; publicar exige confirmación
// explícita y no pueden coexistir dos publicaciones activas con la misma
// vigencia (a diferencia de alimento, el «Precio actual» no bloquea la
// publicación: queda solo como referencia visual).
public class PreciosHuevoHandlerTests
{
    private static readonly DateOnly FechaNotificacion = new(2025, 11, 2);
    private static readonly DateOnly FechaVigencia = new(2025, 11, 10);

    private readonly IRepositorioPublicacionesPreciosHuevo _repositorio =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
    private readonly IImportadorPublicacionPrecioHuevoExcel _importador =
        Substitute.For<IImportadorPublicacionPrecioHuevoExcel>();
    private readonly IAlmacenDocumentosPrecios _almacen = Substitute.For<IAlmacenDocumentosPrecios>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    private ImportarPublicacionPrecioHuevoExcelHandler CrearImportador() =>
        new(_repositorio, _importador, _almacen, _registroVuelo, _unidadTrabajo);

    private PublicarPublicacionPrecioHuevoHandler CrearPublicador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private ActualizarBorradorPrecioHuevoHandler CrearActualizador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private static IReadOnlyList<DatosDetallePrecioHuevo> SeisDetalles(decimal? precioActualControl = 12.30m) =>
        Enum.GetValues<TamanoHuevo>()
            .Select(t => new DatosDetallePrecioHuevo(t, 12.50m, precioActualControl))
            .ToList();

    private static PublicacionPrecioHuevo CrearBorradorPublicable() =>
        new(FechaNotificacion, FechaVigencia, 0.40m, SeisDetalles());

    private static MemoryStream ContenidoExcel() => new("contenido de prueba"u8.ToArray());

    [Fact]
    public async Task ImportarGuardaBorradorConElDocumentoOriginalPrivado()
    {
        var propuesta = new DatosPublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.40m, SeisDetalles());
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionPrecioHuevo(propuesta, []));
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var id = await CrearImportador().Handle(
            new ImportarPublicacionPrecioHuevoExcelCommand(ContenidoExcel()), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        await _almacen.Received(1).GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repositorio.Received(1).Agregar(Arg.Is<PublicacionPrecioHuevo>(p =>
            p.Estado == EstadoPublicacionPrecioHuevo.Borrador &&
            p.Detalles.Count == 6 &&
            p.DocumentoOriginalId != null));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConErroresRechazaCompletoSinPersistir()
    {
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionPrecioHuevo(
            null, [new ErrorImportacionPrecioHuevo(3, "Fila no interpretable.")]));

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            CrearImportador().Handle(
                new ImportarPublicacionPrecioHuevoExcelCommand(ContenidoExcel()), CancellationToken.None));

        Assert.Contains(excepcion.Errors, e => e.ErrorMessage.Contains("Fila 3", StringComparison.Ordinal));
        _repositorio.DidNotReceive().Agregar(Arg.Any<PublicacionPrecioHuevo>());
        await _almacen.DidNotReceive().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublicarRechazaDosPublicacionesConLaMismaVigencia()
    {
        var borrador = CrearBorradorPublicable();
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ExistePublicadaConVigenciaIgualAsync(
                borrador.FechaVigencia, borrador.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearPublicador().Handle(
                new PublicarPublicacionPrecioHuevoCommand(borrador.Id), CancellationToken.None));

        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, borrador.Estado);
        Assert.Equal("Ya existe una publicación activa con esa vigencia.", excepcion.Message);
    }

    [Fact]
    public async Task ActualizarBorradorConIdInexistenteEs404()
    {
        _repositorio.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PublicacionPrecioHuevo?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearActualizador().Handle(
                new ActualizarBorradorPrecioHuevoCommand(
                    Guid.NewGuid(), FechaNotificacion, FechaVigencia, 0.40m, SeisDetalles()),
                CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerPrecioVigenteSinPublicacionDevuelveNull()
    {
        _repositorio.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublicacionPrecioHuevo?)null);

        var detalle = await new ObtenerPrecioHuevoVigenteHandler(_repositorio).Handle(
            new ObtenerPrecioHuevoVigenteQuery(FechaVigencia), CancellationToken.None);

        Assert.Null(detalle);
    }
}
