using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Sink de prueba con los LogEvent reales de GestorCaisy, acotado
/// para no crecer sin límite a lo largo de la suite.</summary>
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
