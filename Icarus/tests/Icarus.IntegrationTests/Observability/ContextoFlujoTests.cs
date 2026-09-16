using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Contexto de identidad del tenant durante toda la ejecución: los
/// eventos internos (operación, decisión y persistencia) y el resumen llevan el
/// ClienteId opaco y el rol validado, sin contaminación entre peticiones
/// concurrentes ni herencia en la anónima (plan del cierre, tarea 3).</summary>
[Collection(IntegracionCollection.Nombre)]
public sealed class ContextoFlujoTests
{
    private const string EventoResumen = "http.request.completed";

    private readonly IdentityFactory _factory;

    public ContextoFlujoTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task LaOperacionDeTenantLlevaClienteIdYRolEnInternosYResumen()
    {
        var token = await LoginComo(SemillaIdentidad.EmailClienteC1);
        var http = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        using var respuesta = await http.SendAsync(
            GranjaAutenticada(token, $"Granja Contexto {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var correlacion = Correlacion(respuesta);
        var eventos = EventosDe(correlacion);

        var internos = eventos
            .Where(e => Prop(e, "EventName") is "operation.started" or "operation.decision"
                or "operation.completed" or "persistence.save_changes.completed")
            .ToList();
        Assert.NotEmpty(internos);
        Assert.All(internos, evento =>
        {
            Assert.Equal(SemillaIdentidad.ClienteC1Id.ToString(), Prop(evento, "ClienteId"));
            Assert.Equal("Cliente", Prop(evento, "Rol"));
        });

        var resumen = Assert.Single(Resumenes(correlacion));
        Assert.Equal(SemillaIdentidad.ClienteC1Id.ToString(), Prop(resumen, "ClienteId"));
        Assert.Equal("Cliente", Prop(resumen, "Rol"));
    }

    [Fact]
    public async Task DosTenantsConcurrentesNoSeContaminanYLaAnonimaNoHereda()
    {
        var tokenC1 = await LoginComo(SemillaIdentidad.EmailClienteC1);
        var tokenDemo = await LoginComo(SemillaIdentidad.EmailCliente);
        var http = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();

        var tareaC1 = http.SendAsync(PedidoAutenticado(HttpMethod.Get, "/api/granjas", tokenC1));
        var tareaDemo = http.SendAsync(PedidoAutenticado(HttpMethod.Get, "/api/granjas", tokenDemo));
        await Task.WhenAll(tareaC1, tareaDemo);
        var anonima = await http.GetAsync("/api/health");

        AssertResumenDeTenant(await tareaC1, SemillaIdentidad.ClienteC1Id);
        AssertResumenDeTenant(await tareaDemo, SemillaIdentidad.ClienteDemoId);
        AssertResumenDeTenant(anonima, null);
    }

    private static void AssertResumenDeTenant(HttpResponseMessage respuesta, Guid? clienteEsperado)
    {
        var correlacion = Correlacion(respuesta);
        var resumen = Assert.Single(Resumenes(correlacion));
        Assert.Equal(clienteEsperado?.ToString(), Prop(resumen, "ClienteId"));
        Assert.Equal(clienteEsperado is null ? null : "Cliente", Prop(resumen, "Rol"));

        // Ningún evento de esta correlación puede traer el tenant de la otra.
        var tenantAjeno = clienteEsperado == SemillaIdentidad.ClienteC1Id
            ? SemillaIdentidad.ClienteDemoId
            : SemillaIdentidad.ClienteC1Id;
        Assert.DoesNotContain(tenantAjeno.ToString(), Serializar(EventosDe(correlacion)));
    }

    private static HttpRequestMessage GranjaAutenticada(string token, string nombre)
    {
        var pedido = PedidoAutenticado(HttpMethod.Post, "/api/granjas", token);
        pedido.Content = JsonContent.Create(new { nombre });
        return pedido;
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

    private static string Serializar(IEnumerable<LogEvent> eventos)
    {
        using var escritor = new StringWriter();
        foreach (var evento in eventos)
            new CompactJsonFormatter().Format(evento, escritor);
        return escritor.ToString();
    }
}
