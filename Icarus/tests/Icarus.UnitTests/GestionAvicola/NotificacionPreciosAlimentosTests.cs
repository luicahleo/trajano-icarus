using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8A Tarea 2 (spec: "Notificación de Precios de Alimentos"): cabecera global
// versionada, inmutable tras publicarse. El precio canónico es el precio final
// por 40 kg (incluye aporte CAISY, fondo y servicios). La identidad del
// producto no incluye la presentación: como máximo un detalle por
// (TipoAlimento, Presentación).
public class NotificacionPreciosAlimentosTests
{
    private static readonly DateOnly FechaDocumento = new(2025, 11, 2);
    private static readonly DateOnly VigenteDesde = new(2025, 11, 10);

    private static DatosDetallePrecio Datos(TipoAlimento tipo, PresentacionAlimento presentacion,
        decimal precio = 180m, int? edadDesde = null, int? edadHasta = null, decimal? precioActualDocumento = null) =>
        new(tipo, presentacion, precio, edadDesde, edadHasta, precioActualDocumento);

    private static NotificacionPreciosAlimentos BorradorConDoceDetalles()
    {
        var tipos = new[]
        {
            TipoAlimento.Preiniciador, TipoAlimento.Iniciador, TipoAlimento.Crecimiento,
            TipoAlimento.Finalizador, TipoAlimento.PosturaUno, TipoAlimento.PosturaDos,
        };
        var detalles = tipos
            .SelectMany(t => new[] { PresentacionAlimento.Bolsa, PresentacionAlimento.Granel }
                .Select(p => Datos(t, p, 180m + (int)t)))
            .ToList();
        return new NotificacionPreciosAlimentos(
            FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m, detalles);
    }

    [Fact]
    public void ElBorradorSeCreaVacioYPuedeActualizarse()
    {
        var notificacion = new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m);

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Borrador, notificacion.Estado);
        Assert.Empty(notificacion.Detalles);

        notificacion.ActualizarBorrador([Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 185m)]);

        Assert.Single(notificacion.Detalles);
        Assert.Equal(1.20m, notificacion.AporteCaisy);
        Assert.Equal(0.60m, notificacion.Fondo);
        Assert.Equal(0.75m, notificacion.Servicios);
    }

    [Fact]
    public void AdmiteDoceCombinacionesUnicas()
    {
        var notificacion = BorradorConDoceDetalles();

        Assert.Equal(12, notificacion.Detalles.Count);
    }

    [Fact]
    public void NoAdmiteDosDetallesConElMismoTipoYPresentacion()
    {
        var detalles = new List<DatosDetallePrecio>
        {
            Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m),
            Datos(TipoAlimento.Iniciador, PresentacionAlimento.Granel, 178m),
            Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 181m),
        };

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m, detalles));

        Assert.Equal("Cada tipo y presentación solo puede tener un precio en la notificación.", excepcion.Message);
    }

    [Fact]
    public void LosImportesDebenSerPositivos()
    {
        var excepcionAportes = Assert.Throws<ReglaNegocioException>(() =>
            new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 0m, 0.60m, 0.75m));
        var excepcionPrecio = Assert.Throws<ReglaNegocioException>(() =>
            new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
                [Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 0m)]));

        Assert.Equal("Los aportes deben ser mayores que cero.", excepcionAportes.Message);
        Assert.Equal("El precio final por 40 kg debe ser mayor que cero.", excepcionPrecio.Message);
    }

    [Fact]
    public void ElRangoDeEdadDebeSerCoherente()
    {
        var rangoInvertido = Assert.Throws<ReglaNegocioException>(() =>
            new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
                [Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m, 60, 30)]));
        var edadNoPositiva = Assert.Throws<ReglaNegocioException>(() =>
            new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
                [Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 180m, 0, 30)]));

        Assert.Equal("El rango de edad debe ser coherente.", rangoInvertido.Message);
        Assert.Equal("El rango de edad debe ser coherente.", edadNoPositiva.Message);
    }

    [Fact]
    public void PublicarSellaNuevoElBorrador()
    {
        var notificacion = BorradorConDoceDetalles();

        notificacion.Publicar();

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Publicada, notificacion.Estado);
        Assert.Throws<ReglaNegocioException>(() =>
            notificacion.ActualizarBorrador([Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 999m)]));
    }

    [Fact]
    public void NoSePublicaUnBorradorSinDetalles()
    {
        var notificacion = new NotificacionPreciosAlimentos(FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m);

        var excepcion = Assert.Throws<ReglaNegocioException>(() => notificacion.Publicar());

        Assert.Equal("La notificación debe tener al menos un detalle de precio.", excepcion.Message);
        Assert.Equal(EstadoNotificacionPreciosAlimentos.Borrador, notificacion.Estado);
    }

    [Fact]
    public void UnaPublicacionFuturaSePuedeAnularYUnaEfectivaNo()
    {
        var futura = BorradorConDoceDetalles();
        futura.Publicar();
        var hoy = futura.VigenteDesde.AddDays(-1);
        var efectiva = BorradorConDoceDetalles();
        efectiva.Publicar();
        var hoyDeLaEfectiva = efectiva.VigenteDesde;

        futura.AnularFutura(hoy);

        Assert.Equal(EstadoNotificacionPreciosAlimentos.Anulada, futura.Estado);
        Assert.Throws<ReglaNegocioException>(() => efectiva.AnularFutura(hoyDeLaEfectiva));
        Assert.Equal(EstadoNotificacionPreciosAlimentos.Publicada, efectiva.Estado);
    }

    [Fact]
    public void ElPrecioActualDelDocumentoSeConservaComoControl()
    {
        var notificacion = new NotificacionPreciosAlimentos(
            FechaDocumento, VigenteDesde, 1.20m, 0.60m, 0.75m,
            [Datos(TipoAlimento.Iniciador, PresentacionAlimento.Bolsa, 185m, 22, 35, 180m)]);

        var detalle = notificacion.Detalles.Single();

        // La columna «Precio actual» del PDF es un control de publicación
        // (spec SP8): se conserva en el detalle, no sustituye al precio final.
        Assert.Equal(180m, detalle.PrecioActualDocumento);
        Assert.Equal(185m, detalle.PrecioFinalPor40Kg);
    }

    [Fact]
    public void UnBorradorNoSePuedeAnular()
    {
        var notificacion = BorradorConDoceDetalles();

        Assert.Throws<ReglaNegocioException>(() => notificacion.AnularFutura(new(2025, 11, 1)));
        Assert.Equal(EstadoNotificacionPreciosAlimentos.Borrador, notificacion.Estado);
    }
}
