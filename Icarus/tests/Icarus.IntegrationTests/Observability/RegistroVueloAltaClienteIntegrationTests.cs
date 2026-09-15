using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Serilog.Events;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>El alta con cuenta narra su recorrido completo en el mismo TraceId
/// y CorrelationId: inicio, decisión, persistencia, transacción, resultado y
/// resumen HTTP, sin datos nominales (plan de Serilog y Seq, tarea 4).</summary>
[Collection(IntegracionCollection.Nombre)]
public sealed class RegistroVueloAltaClienteIntegrationTests
{
    private readonly IdentityFactory _factory;

    public RegistroVueloAltaClienteIntegrationTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task PeticionDelPilotoExponeTraceIdSinExponerDatosDeLaCuenta()
    {
        using var cliente = _factory.CreateClient();
        using var respuesta = await cliente.GetAsync("/api/health");
        var traceId = respuesta.Headers.GetValues("X-Trace-Id").Single();

        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.DoesNotContain("email", traceId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElAltaConCuentaNarraInicioDecisionPersistenciaYResultado()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var http = _factory.CreateClient();
        IdentityFactory.Colector.Limpiar();
        var email = $"flujo-{Guid.NewGuid():N}@icarus.test";

        var alta = new HttpRequestMessage(HttpMethod.Post, "/api/clientes");
        alta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Granja Flujo S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        using var respuesta = await http.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var correlacion = respuesta.Headers.GetValues("X-Correlation-ID").Single();
        var eventos = IdentityFactory.Colector.Eventos
            .Where(e => e.Properties.TryGetValue("CorrelationId", out var valor)
                && valor is ScalarValue { Value: string id } && id == correlacion)
            .ToList();

        var nombres = eventos.Select(e => Prop(e, "EventName")).ToList();
        Assert.Contains("operation.started", nombres);
        Assert.Contains("operation.decision", nombres);
        Assert.Contains("persistence.save_changes.completed", nombres);
        Assert.Contains("operation.completed", nombres);
        Assert.Contains("http.request.completed", nombres);
        Assert.DoesNotContain(email, Serializar(eventos));
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

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private static string Serializar(IEnumerable<LogEvent> eventos) =>
        string.Join(" ", eventos.Select(e =>
            string.Join(" ", e.Properties.Values.Select(v => v.ToString()))));
}
