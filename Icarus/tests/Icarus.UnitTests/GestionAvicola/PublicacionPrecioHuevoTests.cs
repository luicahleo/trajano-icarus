using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9A (spec: "Precios: captura y congelamiento"): cabecera global
// versionada, inmutable tras publicarse. Servicio es un único valor por
// publicación (no por tamaño); PrecioAlProductor ya es el monto que se
// congela en los despachos.
public class PublicacionPrecioHuevoTests
{
    private static readonly DateOnly FechaNotificacion = new(2026, 10, 30);
    private static readonly DateOnly FechaVigencia = new(2026, 11, 1);

    private static DatosDetallePrecioHuevo Datos(
        TamanoHuevo tamano, decimal precio = 0.70m, decimal? precioActualDocumento = null) =>
        new(tamano, precio, precioActualDocumento);

    private static PublicacionPrecioHuevo BorradorConSeisDetalles()
    {
        var tamanos = new[]
        {
            TamanoHuevo.Extra, TamanoHuevo.Primera, TamanoHuevo.Segunda,
            TamanoHuevo.Tercera, TamanoHuevo.Cuarta, TamanoHuevo.Quinta,
        };
        var detalles = tamanos.Select(t => Datos(t, 0.70m + (int)t * 0.01m)).ToList();
        return new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m, detalles);
    }

    [Fact]
    public void ElBorradorSeCreaVacioYPuedeActualizarse()
    {
        var publicacion = new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
        Assert.Empty(publicacion.Detalles);

        publicacion.ActualizarBorrador([Datos(TamanoHuevo.Extra, 0.80m)]);

        Assert.Single(publicacion.Detalles);
        Assert.Equal(0.057m, publicacion.Servicio);
    }

    [Fact]
    public void AdmiteSeisTamanosUnicos()
    {
        var publicacion = BorradorConSeisDetalles();

        Assert.Equal(6, publicacion.Detalles.Count);
    }

    [Fact]
    public void NoAdmiteDosDetallesConElMismoTamano()
    {
        var detalles = new List<DatosDetallePrecioHuevo>
        {
            Datos(TamanoHuevo.Extra, 0.80m),
            Datos(TamanoHuevo.Primera, 0.70m),
            Datos(TamanoHuevo.Extra, 0.81m),
        };

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m, detalles));

        Assert.Equal("Cada tamaño solo puede tener un precio en la publicación.", excepcion.Message);
    }

    [Fact]
    public void ElServicioYLosPreciosDebenSerPositivos()
    {
        var excepcionServicio = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0m));
        var excepcionPrecio = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m,
                [Datos(TamanoHuevo.Extra, 0m)]));

        Assert.Equal("El servicio debe ser mayor que cero.", excepcionServicio.Message);
        Assert.Equal("El precio al productor debe ser mayor que cero.", excepcionPrecio.Message);
    }

    [Fact]
    public void PublicarSellaElBorrador()
    {
        var publicacion = BorradorConSeisDetalles();

        publicacion.Publicar();

        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, publicacion.Estado);
        Assert.Throws<ReglaNegocioException>(() =>
            publicacion.ActualizarBorrador([Datos(TamanoHuevo.Extra, 0.99m)]));
    }

    [Fact]
    public void NoSePublicaUnBorradorSinDetalles()
    {
        var publicacion = new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m);

        var excepcion = Assert.Throws<ReglaNegocioException>(() => publicacion.Publicar());

        Assert.Equal("La publicación debe tener al menos un detalle de precio.", excepcion.Message);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
    }

    [Fact]
    public void UnaPublicacionFuturaSePuedeAnularYUnaEfectivaNo()
    {
        var futura = BorradorConSeisDetalles();
        futura.Publicar();
        var hoy = futura.FechaVigencia.AddDays(-1);
        var efectiva = BorradorConSeisDetalles();
        efectiva.Publicar();
        var hoyDeLaEfectiva = efectiva.FechaVigencia;

        futura.AnularFutura(hoy);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Anulada, futura.Estado);
        Assert.Throws<ReglaNegocioException>(() => efectiva.AnularFutura(hoyDeLaEfectiva));
        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, efectiva.Estado);
    }

    [Fact]
    public void ElPrecioActualDelDocumentoSeConservaComoControl()
    {
        var publicacion = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.057m,
            [Datos(TamanoHuevo.Extra, 0.7954m, 0.7957m)]);

        var detalle = publicacion.Detalles.Single();

        Assert.Equal(0.7957m, detalle.PrecioActualDocumento);
        Assert.Equal(0.7954m, detalle.PrecioAlProductor);
    }

    [Fact]
    public void DescartarBorradorLoDesactivaSinBorrarlo()
    {
        var publicacion = BorradorConSeisDetalles();

        publicacion.DescartarBorrador();

        Assert.False(publicacion.EstaActivo);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
        Assert.Equal(6, publicacion.Detalles.Count);
    }

    [Fact]
    public void DescartarNoAlcanzaAUnaPublicacionNiAUnaAnulada()
    {
        var publicada = BorradorConSeisDetalles();
        publicada.Publicar();

        var excepcion = Assert.Throws<ReglaNegocioException>(() => publicada.DescartarBorrador());

        Assert.Equal("Solo un borrador se puede descartar.", excepcion.Message);
        Assert.True(publicada.EstaActivo);
    }
}
