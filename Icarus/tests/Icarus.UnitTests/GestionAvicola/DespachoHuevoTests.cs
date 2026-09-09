using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class DespachoHuevoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid GranjaId = Guid.NewGuid();
    private static readonly Guid CreadoPor = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static DatosDocumentoNota Documento() => new(
        Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash", "nota.jpg");

    private static DespachoHuevo BorradorConDosDetalles() =>
        new(ClienteId, GranjaId, CreadoPor,
        [
            new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 2, 0),
            new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 1, 90),
        ]);

    private static List<DatosPrecioDespachoHuevo> PreciosPara(DespachoHuevo despacho, Guid publicacionId) =>
        despacho.Detalles.Select(d => new DatosPrecioDespachoHuevo(d.Tamano, 0.70m, publicacionId)).ToList();

    [Fact]
    public void ElBorradorSeCreaConSusLineas()
    {
        var despacho = BorradorConDosDetalles();

        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        Assert.Equal(2, despacho.Detalles.Count);
        Assert.Null(despacho.TotalBs);
    }

    [Fact]
    public void NoAdmiteDosLineasConElMismoTamano()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [
                new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0),
                new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 2, 0),
            ]));

        Assert.Equal("Cada tamaño solo puede aparecer una vez en el despacho.", excepcion.Message);
    }

    [Fact]
    public void UnaLineaSinCantidadEsRechazada()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 0, 0)]));

        Assert.Equal("Cada línea debe declarar una cantidad mayor que cero.", excepcion.Message);
    }

    [Fact]
    public void UnidadesSueltasFueraDeRangoSonRechazadas()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new DespachoHuevo(ClienteId, GranjaId, CreadoPor,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 180)]));

        Assert.Equal("Las unidades sueltas deben estar entre 0 y 179.", excepcion.Message);
    }

    [Fact]
    public void CantidadHuevosSumaAmarrasYSueltas()
    {
        var despacho = BorradorConDosDetalles();

        var extra = despacho.Detalles.Single(d => d.Tamano == TamanoHuevo.Extra);
        var primera = despacho.Detalles.Single(d => d.Tamano == TamanoHuevo.Primera);
        Assert.Equal(360, extra.CantidadHuevos);
        Assert.Equal(270, primera.CantidadHuevos);
        Assert.Equal(3, despacho.TotalAmarras);
        Assert.Equal(630, despacho.TotalHuevos);
    }

    [Fact]
    public void SoloElBorradorSeEditaOSeDesactiva()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());

        Assert.Throws<ReglaNegocioException>(() =>
            despacho.EditarDetalles([new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]));
        Assert.Throws<ReglaNegocioException>(despacho.Desactivar);
    }

    [Fact]
    public void DespacharCongelaPreciosYCalculaTotales()
    {
        var despacho = BorradorConDosDetalles();
        var publicacionId = Guid.NewGuid();

        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, publicacionId), Documento());

        Assert.Equal(EstadoDespachoHuevo.Despachado, despacho.Estado);
        Assert.Equal(new DateOnly(2026, 11, 5), despacho.FechaDespacho);
        Assert.Equal(630 * 0.70m, despacho.TotalBs);
        Assert.All(despacho.Detalles, d => Assert.Equal(publicacionId, d.PublicacionPrecioHuevoId));
        Assert.NotNull(despacho.DocumentoNota);
        Assert.Single(despacho.Historial);
    }

    [Fact]
    public void DespacharSinPrecioParaUnaLineaFallaCompleto()
    {
        var despacho = BorradorConDosDetalles();
        var precioIncompleto = new List<DatosPrecioDespachoHuevo>
        {
            new(TamanoHuevo.Extra, 0.70m, Guid.NewGuid()),
        };

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, precioIncompleto, Documento()));

        Assert.Equal("Falta precio vigente para una línea del despacho.", excepcion.Message);
        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        Assert.Null(despacho.Detalles.First().PrecioProductorCongelado);
    }

    [Fact]
    public void DespacharDosVecesFalla()
    {
        var despacho = BorradorConDosDetalles();
        despacho.Despachar(new DateOnly(2026, 11, 5), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento());

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            despacho.Despachar(new DateOnly(2026, 11, 6), ActorId, PreciosPara(despacho, Guid.NewGuid()), Documento()));

        Assert.Equal("Solo un despacho en borrador se puede enviar.", excepcion.Message);
    }
}
