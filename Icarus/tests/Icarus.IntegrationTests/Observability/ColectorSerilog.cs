using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Sink de prueba que retiene los LogEvent reales serializados por la
/// aplicación, no un capturador de ILogger. Acotado para no crecer sin límite
/// a lo largo de la suite.</summary>
public sealed class ColectorSerilog : ILogEventSink
{
    private const int Limite = 5000;
    private readonly ConcurrentQueue<LogEvent> _eventos = new();

    public IReadOnlyList<LogEvent> Eventos => _eventos.ToArray();

    public void Emit(LogEvent logEvent)
    {
        _eventos.Enqueue(logEvent);
        while (_eventos.Count > Limite)
            _eventos.TryDequeue(out _);
    }

    public void Limpiar()
    {
        while (_eventos.TryDequeue(out _))
        {
            // descarta el evento más antiguo
        }
    }
}
