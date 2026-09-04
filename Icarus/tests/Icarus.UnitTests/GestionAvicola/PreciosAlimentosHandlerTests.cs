using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8A Tarea 3 (spec: "Importación del PDF" y "Modelo"): el borrador se importa
// y se publica con confirmación explícita; la columna «Precio actual» discrepante
// bloquea la publicación (no la extracción); no pueden coexistir dos
// publicaciones activas con la misma vigencia. Los errores de formato rechazan
// la importación completa sin persistencia parcial.
public class PreciosAlimentosHandlerTests
{
    private static readonly DateOnly FechaDocumento = new(2025, 11, 2);
    private static readonly DateOnly VigenteDesde = new(2025, 11, 10);

    private readonly IRepositorioNotificacionesPrecios _repositorio =
        Substitute.For<IRepositorioNotificacionesPrecios>();
    private readonly IImportadorNotificacionPreciosPdf _importador =
        Substitute.For<IImportadorNotificacionPreciosPdf>();
    private readonly IAlmacenDocumentosPrecios _almacen = Substitute.For<IAlmacenDocumentosPrecios>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    private ImportarNotificacionPdfHandler CrearImportador() =>
        new(_repositorio, _importador, _almacen, _registroVuelo, _unidadTrabajo);

    private PublicarNotificacionPreciosHandler CrearPublicador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private ActualizarBorradorPreciosHandler CrearActualizador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private static IReadOnlyList<DatosDetallePrecio> DoceDetalles(decimal? precioActualControl = 180m) =>
        Enum.GetValues<TipoAlimento>()
            .SelectMany(t => new[]
                {
                    new DatosDetallePrecio(t, PresentacionAlimento.Bolsa, 176.5m, 22, 35, precioActualControl),
                    new DatosDetallePrecio(t, PresentacionAlimento.Granel, 174.5m, 22, 35, precioActualControl),
                })
            .ToList();

    private static NotificacionPreciosAlimentos VigentePublicada() =>
        new(new(2025, 10, 1), new(2025, 10, 1), 1.10m, 0.50m, 0.70m,
            [new DatosDetallePrecio(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m, 22, 35, null)]);

    private static NotificacionPreciosAlimentos CrearBorradorPublicable() =>
        new(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m, DoceDetalles());

    private static MemoryStream ContenidoPdf() => new("%PDF-1.4 contenido de prueba"u8.ToArray());

    [Fact]
    public async Task ImportarGuardaBorradorConElDocumentoOriginalPrivado()
    {
        var propuesta = new DatosNotificacionPdf(
            FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m, DoceDetalles());
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionPdf(propuesta, []));
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var id = await CrearImportador().Handle(
            new ImportarNotificacionPdfCommand(ContenidoPdf()), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        await _almacen.Received(1).GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repositorio.Received(1).Agregar(Arg.Is<NotificacionPreciosAlimentos>(n =>
            n.Estado == EstadoNotificacionPreciosAlimentos.Borrador &&
            n.Detalles.Count == 12 &&
            n.DocumentoOriginalId != null));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConErroresRechazaCompletoSinPersistir()
    {
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionPdf(
            null, [new ErrorImportacionPdf(3, "Fila no interpretable.")]));

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            CrearImportador().Handle(new ImportarNotificacionPdfCommand(ContenidoPdf()), CancellationToken.None));

        Assert.Contains(excepcion.Errors, e => e.ErrorMessage.Contains("Fila 3", StringComparison.Ordinal));
        _repositorio.DidNotReceive().Agregar(Arg.Any<NotificacionPreciosAlimentos>());
        await _almacen.DidNotReceive().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarRechazaUnArchivoSinFirmaPdf()
    {
        using var contenido = new MemoryStream("esto no es un pdf"u8.ToArray());

        await Assert.ThrowsAsync<ValidationException>(() =>
            CrearImportador().Handle(new ImportarNotificacionPdfCommand(contenido), CancellationToken.None));

        _importador.DidNotReceive().Importar(Arg.Any<Stream>());
    }

    [Fact]
    public async Task PublicarRechazaDosPublicacionesConLaMismaVigencia()
    {
        var borrador = CrearBorradorPublicable();
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ExistePublicadaConVigenciaIgualAsync(
                borrador.VigenteDesde, borrador.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearPublicador().Handle(new PublicarNotificacionPreciosCommand(borrador.Id), CancellationToken.None));

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Borrador, borrador.Estado);
        Assert.Equal("Ya existe una publicación activa con esa vigencia.", excepcion.Message);
    }

    [Fact]
    public async Task PublicarRechazaUnPrecioActualDiscrepanteConLaVigencia()
    {
        // El documento declara un «Precio actual» de 179 para Iniciador/Bolsa,
        // pero la publicación vigente fija 180: la publicación se bloquea
        // (la extracción del borrador no se ve afectada).
        var borrador = new NotificacionPreciosAlimentos(
            FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
            DoceDetalles(179m));
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ExistePublicadaConVigenciaIgualAsync(
                Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repositorio.ObtenerVigenteAsync(borrador.FechaDocumento, Arg.Any<CancellationToken>())
            .Returns(VigentePublicada());

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            CrearPublicador().Handle(new PublicarNotificacionPreciosCommand(borrador.Id), CancellationToken.None));

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Borrador, borrador.Estado);
        Assert.Contains(excepcion.Errors, e =>
            e.ErrorMessage.Contains("Precio actual", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublicarConControlCoherenteSellaElBorrador()
    {
        var borrador = CrearBorradorPublicable();
        _repositorio.ObtenerPorIdAsync(borrador.Id, Arg.Any<CancellationToken>())
            .Returns(borrador);
        _repositorio.ExistePublicadaConVigenciaIgualAsync(
                Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repositorio.ObtenerVigenteAsync(borrador.FechaDocumento, Arg.Any<CancellationToken>())
            .Returns((NotificacionPreciosAlimentos?)null);

        await CrearPublicador().Handle(
            new PublicarNotificacionPreciosCommand(borrador.Id), CancellationToken.None);

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Publicada, borrador.Estado);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaConcurrenciaOptimistaSeTraduceAConflictoGenerico()
    {
        // La traducción vive en el decorador de la unidad de trabajo
        // (Infrastructure): la capa Application no referencia EF Core.
        var interna = Substitute.For<IUnidadTrabajoGestionAvicola>();
        interna.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var decorador = new UnidadTrabajoConConcurrencia(interna);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            decorador.SaveChangesAsync(CancellationToken.None));

        Assert.Equal("El registro cambió mientras se guardaba; reintente.", excepcion.Message);
    }

    [Fact]
    public async Task PublicarUnaNotificacionInexistenteEs404()
    {
        _repositorio.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NotificacionPreciosAlimentos?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearPublicador().Handle(new PublicarNotificacionPreciosCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarBorradorSoloAlcanzaAlBorrador()
    {
        var publicada = CrearBorradorPublicable();
        publicada.Publicar();
        _repositorio.ObtenerPorIdAsync(publicada.Id, Arg.Any<CancellationToken>())
            .Returns(publicada);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            CrearActualizador().Handle(
                new ActualizarBorradorPreciosCommand(
                    publicada.Id, FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m, DoceDetalles()),
                CancellationToken.None));
    }
}
