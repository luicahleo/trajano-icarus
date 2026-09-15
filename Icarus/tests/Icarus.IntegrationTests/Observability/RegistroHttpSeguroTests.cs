using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Host.Endpoints;
using Icarus.Identity.Infrastructure;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Inspecciona el LogEvent real serializado por Serilog para el
/// resumen HTTP: cobertura de rechazos tempranos, patrón de ruta y ausencia de
/// canarios (plan de Serilog y Seq, tarea 2).</summary>
[Collection(IntegracionCollection.Nombre)]
public class RegistroHttpSeguroTests
{
    private const string EventoResumen = "http.request.completed";

    private readonly IdentityFactory _factory;

    public RegistroHttpSeguroTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task UnaPeticionCompletadaGeneraUnSoloResumenConPatronDeRuta()
    {
        var cliente = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        var respuesta = await cliente.GetAsync("/api/health");
        var correlation = Correlacion(respuesta);

        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("GET", Prop(resumen, "Method"));
        Assert.Equal("/api/health", Prop(resumen, "RoutePattern"));
        Assert.Equal("/api/health", Prop(resumen, "RequestPath"));
        Assert.Equal("200", Prop(resumen, "StatusCode"));
        Assert.True(resumen.Properties.ContainsKey("DurationMs"));
        Assert.Equal("Icarus", Prop(resumen, "Aplicacion"));
        Assert.False(string.IsNullOrEmpty(Prop(resumen, "TraceId")));
    }

    [Fact]
    public async Task UnaRutaNoResueltaUsaUnmatchedSinCopiarElPathname()
    {
        var cliente = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        var respuesta = await cliente.GetAsync("/no-existe-CANARIO.txt");
        var correlation = Correlacion(respuesta);

        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("unmatched", Prop(resumen, "RoutePattern"));
        Assert.Equal("404", Prop(resumen, "StatusCode"));
        Assert.DoesNotContain("CANARIO", Serializar(resumen));
    }

    [Fact]
    public async Task LaQueryYLasCabecerasNoSeFiltranAlEventoSerializado()
    {
        var cliente = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        var pedido = new HttpRequestMessage(HttpMethod.Get,
            "/api/health?canario=CANARIO_QUERY");
        pedido.Headers.Add("X-Canario", "CANARIO_HEADER");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "CANARIO_TOKEN");
        var respuesta = await cliente.SendAsync(pedido);
        var correlation = Correlacion(respuesta);

        var eventos = EventosDe(correlation);
        Assert.NotEmpty(eventos);
        foreach (var evento in eventos)
        {
            var json = Serializar(evento);
            Assert.DoesNotContain("CANARIO", json);
            Assert.DoesNotContain("ClientIP", json);
            Assert.DoesNotContain("UserAgent", json);
        }
    }

    [Fact]
    public async Task ElTraceIdW3cEntranteSeConservaEnElResumen()
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        pedido.Headers.Add("traceparent",
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        var respuesta = await cliente.SendAsync(pedido);
        var correlation = Correlacion(respuesta);

        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", Prop(resumen, "TraceId"));
    }

    [Fact]
    public async Task ElRechazoPorClienteInactivoTambienGeneraResumen()
    {
        var (clienteId, token) = await CrearClienteConSesion();
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var http = _factory.CreateClient();
        var suspender = await http.SendAsync(PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/suspender", admin));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        IdentityFactory.Colector.Limpiar();
        var respuesta = await http.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/health", token));
        var correlation = Correlacion(respuesta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var resumen = Assert.Single(Resumenes(correlation));
        Assert.Equal("401", Prop(resumen, "StatusCode"));
        Assert.Equal("/api/health", Prop(resumen, "RoutePattern"));
    }

    private static List<LogEvent> EventosDe(string correlation) =>
        IdentityFactory.Colector.Eventos
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

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    // Alta embebida de un cliente con cuenta propia y su token ya emitido.
    private async Task<(Guid ClienteId, string Token)> CrearClienteConSesion()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var http = _factory.CreateClient();
        var email = $"observabilidad-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Granja Observabilidad S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await http.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var clienteId = (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        return (clienteId, await LoginComo(email));
    }
}
