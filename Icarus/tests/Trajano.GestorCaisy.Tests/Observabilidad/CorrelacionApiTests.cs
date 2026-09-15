using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Trajano.GestorCaisy.Observabilidad;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Cada envío real de GestorCaisy hacia la API lleva su propio UUID y
/// lo registra como DownstreamCorrelationId, también en refresh y reintento,
/// sin exponer tokens ni cuerpos (plan de Serilog y Seq, tarea 3).</summary>
public class CorrelacionApiTests
{
    private const string BaseApi = "http://api.icarus.test/api/";

    private readonly FakeManejadorHttp _manejador = new();
    private readonly ISesionCaisyActual _sesion = Substitute.For<ISesionCaisyActual>();
    private readonly CapturadorSink _capturador = new();
    private readonly ApiIcarusClient _cliente;

    public CorrelacionApiTests()
    {
        _sesion.AccessToken.Returns("token-actual");
        _sesion.RefreshToken.Returns("refresh-actual");
        _sesion.RenovarTokensAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _sesion.AccessToken.Returns(ci.ArgAt<string>(0));
                _sesion.RefreshToken.Returns(ci.ArgAt<string>(1));
                return Task.CompletedTask;
            });
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiIcarus:BaseUrl"] = BaseApi })
            .Build();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(_capturador)
            .CreateLogger();
        ILoggerFactory fabrica = new SerilogLoggerFactory(logger);
        var registro = fabrica.CreateLogger<CorrelacionApiHandler>();
        var manejador = new CorrelacionApiHandler(registro) { InnerHandler = _manejador };
        _cliente = new ApiIcarusClient(
            new HttpClient(manejador), configuracion, new HttpContextAccessor(), _sesion,
            NullLogger<ApiIcarusClient>.Instance);
    }

    [Fact]
    public async Task CadaEnvioLlevaUnUuidPropioDistintoDelCorrelationIdDelPadre()
    {
        _manejador.Responder(HttpStatusCode.OK, "[]");

        using (LogContext.PushProperty("CorrelationId", "correlacion-del-padre"))
        {
            await _cliente.ListarNotificacionesAsync();
        }

        var correlacionSaliente = _manejador.Peticiones[0].Correlacion;
        Assert.True(Guid.TryParse(correlacionSaliente, out _));
        Assert.NotEqual("correlacion-del-padre", correlacionSaliente);

        var evento = Assert.Single(EventosDeEnvio());
        Assert.Equal(correlacionSaliente, Prop(evento, "DownstreamCorrelationId"));
        Assert.Equal("correlacion-del-padre", Prop(evento, "CorrelationId"));
        Assert.Equal("GET", Prop(evento, "Method"));
        Assert.Equal("/api/precios-alimentos/", Prop(evento, "RoutePattern"));
    }

    [Fact]
    public async Task ElReintentoTas401UsaUnUuidPropioPorEnvio()
    {
        _manejador.Responder(HttpStatusCode.Unauthorized);
        _manejador.Responder(HttpStatusCode.OK,
            """{"accessToken":"access-nuevo","expiraEnSegundos":900}""",
            [new("Set-Cookie", "icarus_refresh=refresh-nuevo; Path=/; HttpOnly")]);
        _manejador.Responder(HttpStatusCode.OK, "[]");

        await _cliente.ListarNotificacionesAsync();

        Assert.Equal(3, _manejador.Peticiones.Count);
        var correlaciones = _manejador.Peticiones.Select(p => p.Correlacion).ToList();
        Assert.All(correlaciones, c => Assert.True(Guid.TryParse(c, out _)));
        Assert.Equal(3, correlaciones.Distinct().Count());
    }

    [Fact]
    public async Task ElEventoDeEnvioNoExponeTokenNiCuerpo()
    {
        _manejador.Responder(HttpStatusCode.OK,
            """[{"id":"6b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c001"}]""");

        await _cliente.ListarNotificacionesAsync();

        foreach (var evento in _capturador.Eventos)
        {
            var texto = string.Join(" ", evento.Properties.Values.Select(v => v.ToString()));
            Assert.DoesNotContain("token-actual", texto);
            Assert.DoesNotContain("refresh-actual", texto);
            Assert.DoesNotContain("6b2e4c46", texto);
        }
    }

    private List<LogEvent> EventosDeEnvio() =>
        _capturador.Eventos.Where(e => Prop(e, "EventName") == CorrelacionApiHandler.EventoEnvio).ToList();

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private sealed class CapturadorSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];
        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }
}
