using System.Net;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

public class LayoutTests
{
    [Fact]
    public async Task LaCabeceraMuestraLaMarcaYLosFavicons()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("rel=\"icon\"", html);
        Assert.Contains("/favicon.svg", html);
        Assert.Contains("/favicon.ico", html);
        Assert.Contains("marca-icarus", html);
        Assert.Contains("Trajano GestorCaisy", html);
    }

    [Fact]
    public async Task LosAssetsDeMarcaSeSirvenDesdeWwwroot()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();

        var svg = await cliente.GetAsync("/favicon.svg");
        Assert.Equal(HttpStatusCode.OK, svg.StatusCode);
        Assert.Contains("viewBox=\"0 0 800 800\"", await svg.Content.ReadAsStringAsync());

        var ico = await cliente.GetAsync("/favicon.ico");
        Assert.Equal(HttpStatusCode.OK, ico.StatusCode);
    }

    [Fact]
    public async Task ElMenuLateralAgrupaAlimentoYHuevoConRotuloDeDominio()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 3);

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("lateral__grupo--alimento", html);
        Assert.Contains("lateral__grupo--huevo", html);
        Assert.Contains("lateral__grupo-titulo\">Alimento</span>", html);
        Assert.Contains("lateral__grupo-titulo\">Huevo</span>", html);
    }

    [Fact]
    public async Task LaCampanitaYElBannerAparecenParaUnGestorConAmbasFunciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 3);

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("data-campana", html);
        Assert.Contains("data-fuente-novedades=\"/Pedidos/Notificaciones/Contador\"", html);
        Assert.Contains("data-fuente-novedades=\"/RecepcionesHuevo/Notificaciones/Contador\"", html);
        Assert.Contains("data-banner-notificaciones", html);
        Assert.Contains("Activar notificaciones", html);
    }

    // Todas las páginas operativas exigen una funcionalidad de CAISY, así que
    // un gestor sin ninguna no puede llegar a /Precios: la política lo manda a
    // /Sesion/Denegado. Esa página sí renderiza el layout autenticado, y ahí
    // se comprueba que ni la campanita ni el banner se pintan sin funciones.
    [Fact]
    public async Task LaCampanitaNoAparecePorFueraDeLasFuncionesDeCaisy()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(rol: "GestorCaisy", funcCaisy: 0);

        var html = await cliente.GetStringAsync("/Sesion/Denegado");

        Assert.DoesNotContain("data-campana", html);
        Assert.DoesNotContain("data-banner-notificaciones", html);
    }
}
