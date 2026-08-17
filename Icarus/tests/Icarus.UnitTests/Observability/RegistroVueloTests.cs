using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Icarus.UnitTests.Observability;

public sealed class RegistroVueloTests
{
    [Fact]
    public void RegistraInicioYFinConCamposEstablesYDescartaCamposNoPermitidos()
    {
        var logger = new LoggerCapturador();
        var registro = new RegistroVuelo(logger);
        var descriptor = new DescriptorOperacionRegistroVuelo(
            "clientes.prueba", new Dictionary<string, DatoRegistroVuelo>
            {
                ["Available"] = DatoRegistroVuelo.Booleano,
                ["Count"] = DatoRegistroVuelo.Entero,
            });

        using var operacion = registro.Iniciar(descriptor);
        operacion.Decidir("available", "succeeded", new Dictionary<string, object?>
        {
            ["Available"] = true,
            ["Count"] = 1,
            ["Email"] = "no-debe-aparecer",
        });
        operacion.Completar();

        Assert.Equal(
            ["operation.started", "operation.decision", "operation.completed"],
            logger.Eventos.Select(e => e.EventName));
        Assert.DoesNotContain("no-debe-aparecer", logger.Eventos.SelectMany(e => e.Properties.Values));
        Assert.Equal("clientes.prueba", logger.Eventos[0].Properties["Operation"]);
        Assert.Equal("succeeded", logger.Eventos[^1].Properties["Outcome"]);
        Assert.Contains("DurationMs", logger.Eventos[^1].Properties.Keys);
    }

    [Fact]
    public void CamposDeTipoIncorrectoYProhibidosSeOmiten()
    {
        var logger = new LoggerCapturador();
        var registro = new RegistroVuelo(logger);
        var descriptor = new DescriptorOperacionRegistroVuelo(
            "clientes.prueba", new Dictionary<string, DatoRegistroVuelo>
            {
                ["Available"] = DatoRegistroVuelo.Booleano,
            });

        using var operacion = registro.Iniciar(descriptor);
        operacion.Decidir("available", "succeeded", new Dictionary<string, object?>
        {
            ["Available"] = "true",
            ["Password"] = "secreto",
            ["TrabajadorId"] = Guid.NewGuid(),
        });

        var evento = Assert.Single(logger.Eventos, e => e.EventName == "operation.decision");
        Assert.DoesNotContain("Available", evento.Properties.Keys);
        Assert.DoesNotContain("Password", evento.Properties.Keys);
        Assert.DoesNotContain("TrabajadorId", evento.Properties.Keys);
    }

    private sealed class LoggerCapturador : ILogger
    {
        public List<EventoCapturado> Eventos { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> propiedades &&
                propiedades.FirstOrDefault(p => p.Key == "EventName") is { Value: string eventName })
            {
                Eventos.Add(new EventoCapturado(eventName,
                    propiedades.Where(p => p.Key != "{OriginalFormat}")
                        .ToDictionary(p => p.Key, p => p.Value)));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed record EventoCapturado(string EventName, Dictionary<string, object?> Properties);
}
