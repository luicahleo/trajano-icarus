using System.Net;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Trajano.GestorCaisy.Tests.Ayudas;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Resumen HTTP real de GestorCaisy: un evento por petición externa,
/// patrón de ruta conservado tras reejecutar el error y sin canarios.</summary>
public class RegistroHttpSeguroTests
{
    private const string EventoResumen = "http.request.completed";
    private const string PatronControlador = "{controller=Precios}/{action=Index}/{id?}";

    [Fact]
    public async Task UnaPeticionMvcGeneraUnSoloResumenConPatronDeRuta()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        var respuesta = await cliente.GetAsync("/Sesion/Acceder");
        var correlation = Correlacion(respuesta);

        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("GET", Prop(resumen, "Method"));
        Assert.Equal(PatronControlador, Prop(resumen, "RoutePattern"));
        Assert.Equal(PatronControlador, Prop(resumen, "RequestPath"));
        Assert.Equal("200", Prop(resumen, "StatusCode"));
        Assert.Equal("Trajano.GestorCaisy", Prop(resumen, "Aplicacion"));
        Assert.False(string.IsNullOrEmpty(Prop(resumen, "TraceId")));
    }

    [Fact]
    public async Task UnError404ConservaLaRutaOriginalTrasReejecutar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        var respuesta = await cliente.GetAsync("/ruta-inexistente-CANARIO");
        var correlation = Correlacion(respuesta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("unmatched", Prop(resumen, "RoutePattern"));
        Assert.Equal("404", Prop(resumen, "StatusCode"));
        Assert.DoesNotContain("CANARIO", Serializar(resumen));
    }

    [Fact]
    public async Task LaQueryYLasCabecerasNoSeFiltranAlEventoSerializado()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        var pedido = new HttpRequestMessage(HttpMethod.Get,
            "/Sesion/Acceder?canario=CANARIO_QUERY");
        pedido.Headers.Add("X-Canario", "CANARIO_HEADER");
        var respuesta = await cliente.SendAsync(pedido);
        var correlation = Correlacion(respuesta);

        var eventos = EventosDe(correlation);
        Assert.NotEmpty(eventos);
        foreach (var evento in eventos)
            Assert.DoesNotContain("CANARIO", Serializar(evento));
    }

    private static List<LogEvent> EventosDe(string correlation) =>
        AplicacionDePruebas.Colector.Eventos
            .Where(e => e.Properties.TryGetValue("CorrelationId", out var valor)
                && valor is ScalarValue { Value: string id } && id == correlation)
            .ToList();

    private static List<LogEvent> Resumenes(string correlation) =>
        EventosDe(correlation).Where(e => Prop(e, "EventName") == EventoResumen).ToList();

    private static string Correlacion(HttpResponseMessage respuesta) =>
        respuesta.Headers.TryGetValues("X-Correlation-ID", out var valores)
            ? valores.Single()
            : throw new InvalidOperationException("La respuesta no trae X-Correlation-ID.");

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private static string Serializar(LogEvent evento)
    {
        using var escritor = new StringWriter();
        new CompactJsonFormatter().Format(evento, escritor);
        return escritor.ToString();
    }
}
