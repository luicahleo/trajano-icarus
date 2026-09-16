using System.Net;
using System.Text.RegularExpressions;
using Icarus.Identity.Infrastructure;
using Serilog.Events;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Correlación de extremo a extremo con los dos hosts reales y
/// transporte HTTP real: la petición MVC y la llamada a la API comparten
/// TraceId, cada salto lleva su UUID y el DownstreamCorrelationId del envío
/// coincide con el CorrelationId que recibe la API. Sin cliente falso ni IDs
/// de traza inyectados por la prueba (plan del cierre, tarea 4).</summary>
[Collection(IntegracionCollection.Nombre)]
public sealed class CorrelacionExtremoAExtremoTests : IAsyncLifetime
{
    private const string EventoResumen = "http.request.completed";
    private const string EventoEnvio = "http.client.send";
    private const string PlantillaNotificaciones = "/api/precios-alimentos/";
    private const string PlantillaRenovar = "/api/identidad/sesion/renovar";

    private readonly IdentityFactory _sql;
    private HostsObservabilidadFixture _hosts = null!;

    public CorrelacionExtremoAExtremoTests(IdentityFactory sql) => _sql = sql;

    public async Task InitializeAsync()
    {
        _hosts = new HostsObservabilidadFixture(_sql.CadenaConexion);
        await _hosts.InitializeAsync();
    }

    public Task DisposeAsync() => _hosts.DisposeAsync();

    [Fact]
    public async Task LaPeticionMvcYLaLlamadaApiCompartenTrazaYCorrelacion()
    {
        Limpiar();

        var respuesta = await AccederAsync();

        Assert.True(respuesta.IsSuccessStatusCode,
            $"El acceso terminó con {respuesta.StatusCode}.");
        var traceId = TraceId(respuesta);
        var envio = Assert.Single(EnviosMvc(traceId, PlantillaNotificaciones));
        var resumenApi = Assert.Single(ResumenesApi(traceId, PlantillaNotificaciones));
        Assert.Equal(Prop(envio, "DownstreamCorrelationId"), Prop(resumenApi, "CorrelationId"));
        Assert.Equal(traceId, Prop(resumenApi, "TraceId"));
        Assert.NotEmpty(ResumenesMvc(traceId));
    }

    [Fact]
    public async Task ElCiclo401RenuevaYReintentaConTresEnviosYUnaTraza()
    {
        Limpiar();
        ForzarUn401StartupFilter.Activar();

        var respuesta = await AccederAsync();

        Assert.True(respuesta.IsSuccessStatusCode,
            $"El acceso terminó con {respuesta.StatusCode}.");
        var traceId = TraceId(respuesta);
        var envios = _hosts.ColectorMvc.Eventos
            .Where(e => Prop(e, "EventName") == EventoEnvio && Prop(e, "TraceId") == traceId)
            .ToList();
        Assert.Equal(3, envios.Count);
        Assert.Equal(3, envios.Select(e => Prop(e, "DownstreamCorrelationId")).Distinct().Count());
        Assert.Contains(PlantillaRenovar, envios.Select(e => Prop(e, "RoutePattern")));
        Assert.Equal(2, envios.Count(e => Prop(e, "RoutePattern") == PlantillaNotificaciones));

        var reintento = envios[^1];
        Assert.Equal(PlantillaNotificaciones, Prop(reintento, "RoutePattern"));
        var resumenApi = Assert.Single(ResumenesApi(traceId, PlantillaNotificaciones));
        Assert.Equal(Prop(reintento, "DownstreamCorrelationId"), Prop(resumenApi, "CorrelationId"));
    }

    private void Limpiar()
    {
        _hosts.ColectorMvc.Limpiar();
        _hosts.ColectorApi.Limpiar();
    }

    private List<LogEvent> EnviosMvc(string traceId, string plantilla) =>
        _hosts.ColectorMvc.Eventos
            .Where(e => Prop(e, "EventName") == EventoEnvio
                && Prop(e, "TraceId") == traceId
                && Prop(e, "RoutePattern") == plantilla)
            .ToList();

    private List<LogEvent> ResumenesMvc(string traceId) =>
        _hosts.ColectorMvc.Eventos
            .Where(e => Prop(e, "EventName") == EventoResumen && Prop(e, "TraceId") == traceId)
            .ToList();

    private List<LogEvent> ResumenesApi(string traceId, string plantilla) =>
        _hosts.ColectorApi.Eventos
            .Where(e => Prop(e, "EventName") == EventoResumen
                && Prop(e, "TraceId") == traceId
                && Prop(e, "RoutePattern") == plantilla)
            .ToList();

    // El POST de acceso sigue la redirección y termina en /Precios dentro del
    // mismo envío, donde el cliente MVC ya llama a la API real.
    private async Task<HttpResponseMessage> AccederAsync()
    {
        var html = await _hosts.Mvc.GetStringAsync("/Sesion/Acceder");
        var token = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<valor>[^\"]+)\"").Groups["valor"].Value;
        return await _hosts.Mvc.PostAsync("/Sesion/Acceder", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = SemillaIdentidad.EmailGestorPedidoAlimento,
                ["Contrasena"] = IdentityFactory.ContrasenaDePrueba,
                ["__RequestVerificationToken"] = token,
            }));
    }

    private static string TraceId(HttpResponseMessage respuesta) =>
        respuesta.Headers.TryGetValues("X-Trace-Id", out var valores)
            ? valores.Single()
            : throw new InvalidOperationException("La respuesta MVC no trae X-Trace-Id.");

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;
}
