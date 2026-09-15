using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Composición real de la sección Serilog de GestorCaisy: el
/// desarrollo no debe suprimir Information y las propiedades globales deben
/// coincidir con las de la API (plan de Serilog y Seq, tarea 1).</summary>
public sealed class ConfiguracionSerilogTests
{
    [Fact]
    public void ResuelveLasMismasPropiedadesGlobalesQueLaApi()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Testing", incluirDesarrollo: false)
            .WriteTo.Sink(capturador).CreateLogger();

        logger.Information("evento de prueba");

        var evento = Assert.Single(capturador.Eventos);
        Assert.Equal("Trajano.GestorCaisy", Valor(evento, "Aplicacion"));
        Assert.Equal("Testing", Valor(evento, "Entorno"));
        Assert.Equal("development", Valor(evento, "Release"));
        Assert.True(evento.Properties.ContainsKey("EnvironmentName"));
        Assert.True(evento.Properties.ContainsKey("MachineName"));
        Assert.True(evento.Properties.ContainsKey("ThreadId"));
    }

    [Fact]
    public void ElDesarrolloNoSuprimeInformationPropio()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Development", incluirDesarrollo: true)
            .WriteTo.Sink(capturador).CreateLogger();

        logger.Information("flujo propio de oficina");

        var evento = Assert.Single(capturador.Eventos);
        Assert.Equal("flujo propio de oficina", evento.RenderMessage());
    }

    [Fact]
    public void ElDesarrolloDeclaraSeqConEntradaNombrada()
    {
        var configuracion = Construir("Development", incluirDesarrollo: true, evitarRed: false);
        var seq = configuracion.GetSection("Serilog:WriteTo:Seq");

        Assert.Equal("Seq", seq["Name"]);
        Assert.Equal("http://localhost:5341", seq["Args:serverUrl"]);
    }

    private static string? Valor(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private static LoggerConfiguration Componer(string entorno, bool incluirDesarrollo) =>
        new LoggerConfiguration().ReadFrom.Configuration(
            Construir(entorno, incluirDesarrollo, evitarRed: true));

    private static IConfiguration Construir(string entorno, bool incluirDesarrollo, bool evitarRed)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);
        if (incluirDesarrollo)
            builder.AddJsonFile("appsettings.Development.json", optional: false);
        var fuentes = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = entorno,
        };
        if (evitarRed && incluirDesarrollo)
            fuentes["Serilog:WriteTo:Seq:Args:serverUrl"] = "http://127.0.0.1:1";
        builder.AddInMemoryCollection(fuentes);
        return builder.Build();
    }

    private sealed class CapturadorSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];
        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }
}
