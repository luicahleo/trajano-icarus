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
}
