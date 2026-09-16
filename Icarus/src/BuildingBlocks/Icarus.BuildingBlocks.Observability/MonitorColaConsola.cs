using System.Diagnostics.Metrics;
using Serilog.Sinks.Async;

namespace Icarus.BuildingBlocks.Observability;

/// <summary>Monitor mínimo de la cola asíncrona de consola: publica capacidad,
/// ocupación y descartes como métricas genéricas de .NET y conserva el
/// inspector para diagnósticos. No emite avisos por la cola saturada: el aviso
/// viajaría por la propia cola que está descartando.</summary>
public sealed class MonitorColaConsola : IAsyncLogEventSinkMonitor, IDisposable
{
    public const string NombreMedidor = "Icarus.Observabilidad.ColaConsola";

    private readonly Meter _medidor = new(NombreMedidor);

    public MonitorColaConsola()
    {
        _medidor.CreateObservableGauge("cola.consola.capacidad", () => Inspector?.BufferSize ?? 0);
        _medidor.CreateObservableGauge("cola.consola.ocupacion", () => Inspector?.Count ?? 0);
        _medidor.CreateObservableGauge("cola.consola.descartes", () => Inspector?.DroppedMessagesCount ?? 0);
    }

    public IAsyncLogEventSinkInspector? Inspector { get; private set; }

    public void StartMonitoring(IAsyncLogEventSinkInspector inspector) => Inspector = inspector;

    public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        if (ReferenceEquals(Inspector, inspector))
            Inspector = null;
    }

    public void Dispose() => _medidor.Dispose();
}
