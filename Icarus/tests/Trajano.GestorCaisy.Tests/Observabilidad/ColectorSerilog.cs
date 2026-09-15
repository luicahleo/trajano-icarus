using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Sink de prueba con los LogEvent reales de un host, acotado para no
/// crecer sin límite a lo largo de la suite. Es por instancia de host: dos
/// escenarios no comparten eventos.</summary>
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

    /// <summary>Serializa la totalidad de los eventos capturados, incluidos
    /// mensaje renderizado, propiedades y excepción. Sin filtrar por
    /// CorrelationId ni descartar fuentes del framework.</summary>
    public string SerializarTodo() =>
        string.Join('\n', Eventos.Select(Serializar));

    public static string Serializar(LogEvent evento)
    {
        using var escritor = new StringWriter();
        new CompactJsonFormatter().Format(evento, escritor);
        return escritor.ToString();
    }
}
