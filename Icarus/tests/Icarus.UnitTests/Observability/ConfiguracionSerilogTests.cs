using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Icarus.UnitTests.Observability;

/// <summary>Composición real de la sección Serilog de la API, no capturadores
/// de ILogger: comprueba enrichers, overrides y saneamiento del evento
/// serializado (plan de Serilog y Seq, tarea 1).</summary>
public sealed class ConfiguracionSerilogTests
{
    [Fact]
    public void ResuelvePropiedadesGlobalesYResuelveElEntornoSinFiltrarElRelease()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Testing", new Dictionary<string, string?>
        {
            ["Serilog:Properties:Release"] = "../secreto raro",
        }).WriteTo.Sink(capturador).CreateLogger();

        logger.Information("evento de prueba");

        var evento = Assert.Single(capturador.Eventos);
        Assert.Equal("Icarus", Valor(evento, "Aplicacion"));
        Assert.Equal("Testing", Valor(evento, "Entorno"));
        Assert.Equal("..secretoraro", Valor(evento, "Release"));
        Assert.True(evento.Properties.ContainsKey("EnvironmentName"));
        Assert.True(evento.Properties.ContainsKey("MachineName"));
        Assert.True(evento.Properties.ContainsKey("ThreadId"));
    }

    [Fact]
    public void LosOverridesDeMicrosoftGobiernanElNivelDeSerilog()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Testing")
            .WriteTo.Sink(capturador).CreateLogger();

        logger.Information("propio");
        logger.ForContext(Constants.SourceContextPropertyName,
                "Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware")
            .Information("ruido del framework");

        var evento = Assert.Single(capturador.Eventos);
        Assert.Equal("propio", evento.RenderMessage());
    }

    [Fact]
    public void CambiarElNivelMinimoEnConfiguracionCambiaElComportamiento()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Testing", new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }).WriteTo.Sink(capturador).CreateLogger();

        logger.Information("se filtra");
        logger.Warning("se conserva");

        var evento = Assert.Single(capturador.Eventos);
        Assert.Equal(LogEventLevel.Warning, evento.Level);
    }

    [Fact]
    public void LaBaseDeclaraUnaSolaConsolaEnvueltaEnAsync()
    {
        var configuracion = Construir("Testing");
        var writeTo = configuracion.GetSection("Serilog:WriteTo");

        Assert.Equal(["Consola"], writeTo.GetChildren().Select(c => c.Key));
        Assert.Equal("Async", writeTo["Consola:Name"]);
        Assert.Equal("Console", writeTo["Consola:Args:configure:0:Name"]);
        Assert.Equal("False", writeTo["Consola:Args:blockWhenFull"]);
    }

    [Fact]
    public void ElEntornoDeDesarrolloDeclaraSeqConEntradaNombrada()
    {
        var configuracion = Construir("Development",
            incluirDesarrollo: true);
        var seq = configuracion.GetSection("Serilog:WriteTo:Seq");

        Assert.Equal("Seq", seq["Name"]);
        Assert.Equal("http://localhost:5341", seq["Args:serverUrl"]);
    }

    [Fact]
    public void UnSoloEventoSeEntregaUnaVezSinDuplicarSinks()
    {
        var capturador = new CapturadorSink();
        using var logger = Componer("Testing")
            .WriteTo.Sink(capturador).CreateLogger();

        logger.Information("una vez");

        Assert.Single(capturador.Eventos);
    }

    private static string? Valor(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private static LoggerConfiguration Componer(
        string entorno, IReadOnlyDictionary<string, string?>? overrides = null) =>
        new LoggerConfiguration().ReadFrom.Configuration(Construir(entorno, overrides: overrides));

    private static IConfiguration Construir(
        string entorno, bool incluirDesarrollo = false,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var fuentes = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = entorno,
        };
        if (overrides is not null)
            foreach (var par in overrides) fuentes[par.Key] = par.Value;

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(fuentes);
        if (incluirDesarrollo)
            builder.AddJsonFile("appsettings.Development.json", optional: false);
        return builder.Build();
    }

    private sealed class CapturadorSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];
        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }
}
