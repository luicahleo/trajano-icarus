using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;
using FechasNegocio = Icarus.GestionAvicola.Application.PreciosHuevo.FechasNegocio;

namespace Icarus.UnitTests.GestionAvicola;

// SP9D (spec: "Corrección de una publicación vigente"): la vista previa es de
// solo lectura y calcula los mismos montos que la corrección real. Cuando una
// línea congelada con la errónea usa un tamaño que la correctiva no cubre, el
// ajuste la ignora (continue) y la línea conservaría el precio erróneo en
// silencio: la vista previa debe mostrar esos tamaños para que el gestor lo
// decida a conciencia.
public class PrevisualizarCorreccionPrecioHuevoHandlerTests
{
    private static readonly DateOnly FechaNotificacion = FechasNegocio.Hoy().AddDays(-15);
    private static readonly DateOnly FechaVigencia = FechasNegocio.Hoy().AddDays(-10);

    private readonly IRepositorioPublicacionesPreciosHuevo _repositorioPrecios =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
    private readonly IRepositorioDespachosHuevo _repositorioDespachos =
        Substitute.For<IRepositorioDespachosHuevo>();

    private PrevisualizarCorreccionPrecioHuevoHandler CrearHandler() =>
        new(_repositorioPrecios, _repositorioDespachos);

    private static PublicacionPrecioHuevo PublicacionVigente(decimal precioExtra, decimal servicio = 0.05m)
    {
        var publicacion = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, servicio, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, precioExtra)]);
        publicacion.Publicar();
        return publicacion;
    }

    private static DespachoHuevo DespachoRecibidoQueUso(Guid publicacionId)
    {
        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        despacho.Despachar(FechaVigencia, Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Extra, 0.75m, publicacionId)],
            new DatosDocumentoNota(Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash", "nota.jpg"));
        despacho.ConfirmarRecepcion(FechaVigencia.AddDays(1), Guid.NewGuid());
        return despacho;
    }

    [Fact]
    public async Task UnTamanoSinPrecioCorrectivoApareceEnLaListaDeNoCompensables()
    {
        var erronea = PublicacionVigente(0.75m);
        // La correctiva solo cubre Primera: la línea Extra congelada con la
        // errónea queda sin compensación.
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 0.85m)]);
        var despacho = DespachoRecibidoQueUso(erronea.Id);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        var previa = await CrearHandler().Handle(
            new PrevisualizarCorreccionPrecioHuevoQuery(erronea.Id, correctiva.Id), CancellationToken.None);

        Assert.Contains("Extra", previa.TamanosSinPrecioCorrectivo);
        // Sin precio correctivo no hay ajuste para ese despacho.
        Assert.Empty(previa.Ajustes);
        Assert.Equal(0m, previa.Total);
    }

    [Fact]
    public async Task TamanosCubiertosPorLaCorrectivaNoAparecenEnLaLista()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        var despacho = DespachoRecibidoQueUso(erronea.Id);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        var previa = await CrearHandler().Handle(
            new PrevisualizarCorreccionPrecioHuevoQuery(erronea.Id, correctiva.Id), CancellationToken.None);

        Assert.Empty(previa.TamanosSinPrecioCorrectivo);
        Assert.Single(previa.Ajustes);
    }
}
