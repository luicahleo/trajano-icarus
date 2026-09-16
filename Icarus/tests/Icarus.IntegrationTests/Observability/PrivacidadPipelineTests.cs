using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Ningún evento de la ejecución conserva el pathname concreto: el
/// scope de ruta sombrea RequestPath con el patrón seguro, incluso en rutas con
/// segmentos dinámicos, y la ruta desconocida queda como unmatched (plan del
/// cierre, tarea 3).</summary>
[Collection(IntegracionCollection.Nombre)]
public sealed class PrivacidadPipelineTests
{
    private const string EventoResumen = "http.request.completed";
    private const string PatronGalpones = "/api/granjas/{granjaId:guid}/galpones";

    private readonly IdentityFactory _factory;

    public PrivacidadPipelineTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task LaRutaConcretaNoApareceEnNingunEventoDeLaEjecucion()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var http = _factory.CreateClient();
        var granjaId = await ObtenerGranjaDemoAsync(http, token);
        IdentityFactory.Colector.Limpiar();

        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/granjas/{granjaId}/galpones", token);
        pedido.Content = JsonContent.Create(new
        {
            numero = $"G{Guid.NewGuid():N}"[..8],
            capacidadMaxima = 1000,
            gallinasActuales = 100,
            fechaNacimientoLote = "2026-01-01",
            descripcion = (string?)null,
        });
        using var respuesta = await http.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var correlacion = Correlacion(respuesta);
        Assert.DoesNotContain(granjaId.ToString(), SerializarTodo());

        var resumen = Assert.Single(Resumenes(correlacion));
        Assert.Equal(PatronGalpones, Prop(resumen, "RoutePattern"));
        Assert.Equal(PatronGalpones, Prop(resumen, "RequestPath"));

        var internos = EventosDe(correlacion)
            .Where(e => Prop(e, "EventName") is "operation.started" or "operation.decision"
                or "operation.completed" or "persistence.save_changes.completed")
            .ToList();
        Assert.NotEmpty(internos);
        Assert.All(internos, evento =>
        {
            Assert.Equal(PatronGalpones, Prop(evento, "RoutePattern"));
            Assert.Equal(PatronGalpones, Prop(evento, "RequestPath"));
        });
    }

    [Fact]
    public async Task LaRutaDesconocidaQuedaComoUnmatchedSinCopiarElPathname()
    {
        var http = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        var respuesta = await http.GetAsync("/ruta-inexistente-CANARIO.txt");

        var correlacion = Correlacion(respuesta);
        var resumen = Assert.Single(Resumenes(correlacion));
        Assert.Equal("unmatched", Prop(resumen, "RoutePattern"));
        Assert.DoesNotContain("CANARIO", SerializarTodo());
    }

    private static async Task<Guid> ObtenerGranjaDemoAsync(HttpClient http, string token)
    {
        using var respuesta = await http.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/granjas", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var granjas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return granjas.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
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

    private static string SerializarTodo()
    {
        using var escritor = new StringWriter();
        foreach (var evento in IdentityFactory.Colector.Eventos)
            new CompactJsonFormatter().Format(evento, escritor);
        return escritor.ToString();
    }
}
