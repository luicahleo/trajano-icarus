using Icarus.BuildingBlocks.Observability;
using Microsoft.Extensions.Logging;

namespace Icarus.UnitTests.Observability;

public sealed class PersistenciaRegistroVueloTests
{
    [Fact]
    public void SaveChangesYTransaccionUsanEventosDistintosYNoIncluyenEstadoDeEntidades()
    {
        var logger = new Capturador();
        var registro = new RegistroVuelo(logger);

        registro.PersistenciaCompletada("Clientes", 2, 7);
        registro.PersistenciaFallida("Identity", 3);
        registro.TransaccionTerminada("Clientes", true);
        registro.TransaccionTerminada("Identity", false);

        Assert.Contains(logger.Eventos, e => e.Nombre == "persistence.save_changes.completed"
            && Equals(e.Propiedades["PersistenceContext"], "Clientes")
            && Equals(e.Propiedades["RowsAffected"], 2));
        Assert.Contains(logger.Eventos, e => e.Nombre == "persistence.save_changes.failed"
            && Equals(e.Propiedades["PersistenceContext"], "Identity"));
        Assert.Contains(logger.Eventos, e => e.Nombre == "transaction.committed");
        Assert.Contains(logger.Eventos, e => e.Nombre == "transaction.rolled_back");
        Assert.DoesNotContain(logger.Eventos.SelectMany(e => e.Propiedades.Keys),
            clave => clave is "Entity" or "Sql" or "Parameters" or "ChangeTracker");
    }

    private sealed class Capturador : ILogger
    {
        public List<Evento> Eventos { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Nulo.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> propiedades &&
                propiedades.FirstOrDefault(p => p.Key == "EventName").Value is string nombre)
                Eventos.Add(new(nombre, propiedades.ToDictionary(p => p.Key, p => p.Value)));
        }
        private sealed class Nulo : IDisposable
        {
            public static readonly Nulo Instance = new();
            public void Dispose() { }
        }
    }
    private sealed record Evento(string Nombre, Dictionary<string, object?> Propiedades);
}
