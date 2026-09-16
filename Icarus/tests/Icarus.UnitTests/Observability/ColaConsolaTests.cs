using System.Collections.Concurrent;
using Icarus.BuildingBlocks.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Icarus.UnitTests.Observability;

/// <summary>Cola asíncrona de consola: el productor no se bloquea, los descartes
/// son observables con el inspector y la cola se drena y vuelve a aceptar
/// eventos al liberar el consumidor (plan del cierre, tarea 5).</summary>
public sealed class ColaConsolaTests
{
    [Fact]
    public async Task LaColaNoBloqueaAlProductorYLosDescartesSonObservables()
    {
        using var consumidor = new SinkBloqueable();
        using var monitor = new MonitorColaConsola();
        using var logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.Sink(consumidor), bufferSize: 8, blockWhenFull: false, monitor: monitor)
            .CreateLogger();

        consumidor.Bloquear();
        logger.Information("evento de calentamiento");
        Assert.True(consumidor.EsperandoConsumidor(TimeSpan.FromSeconds(15)),
            "El consumidor no arrancó.");

        var productor = Task.Run(() =>
        {
            for (var i = 0; i < 5000; i++)
                logger.Information("evento {Indice}", i);
        });

        var termino = await Task.WhenAny(productor, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(productor, termino);

        var inspector = monitor.Inspector
            ?? throw new InvalidOperationException("El monitor no recibió el inspector.");
        Assert.Equal(8, inspector.BufferSize);
        Assert.True(inspector.DroppedMessagesCount > 0, "No se observaron descartes.");

        consumidor.Liberar();
        Assert.True(SpinWait.SpinUntil(() => inspector.Count == 0, TimeSpan.FromSeconds(15)),
            "La cola no se drenó.");
        logger.Information("evento posterior");
        Assert.True(consumidor.EsperarEmision("evento posterior", TimeSpan.FromSeconds(15)),
            "El evento posterior no se emitió.");
    }

    [Fact]
    public async Task ConPoliticaBloqueanteElProductorSiSeBloquea()
    {
        using var consumidor = new SinkBloqueable();
        using var logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.Sink(consumidor), bufferSize: 4, blockWhenFull: true)
            .CreateLogger();

        consumidor.Bloquear();
        var productor = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
                logger.Information("evento {Indice}", i);
        });

        var termino = await Task.WhenAny(productor, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(productor, termino);

        consumidor.Liberar();
        await productor.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class SinkBloqueable : ILogEventSink, IDisposable
    {
        private readonly ManualResetEventSlim _liberar = new(true);
        private readonly ConcurrentQueue<string> _emitidos = new();
        private int _enProceso;

        public void Bloquear() => _liberar.Reset();

        public void Liberar() => _liberar.Set();

        public bool EsperandoConsumidor(TimeSpan espera) =>
            SpinWait.SpinUntil(() => Volatile.Read(ref _enProceso) > 0, espera);

        public bool EsperarEmision(string mensaje, TimeSpan espera) =>
            SpinWait.SpinUntil(() => _emitidos.Contains(mensaje), espera);

        public void Emit(LogEvent logEvent)
        {
            Interlocked.Increment(ref _enProceso);
            try
            {
                _liberar.Wait(TimeSpan.FromSeconds(30));
            }
            finally
            {
                Interlocked.Decrement(ref _enProceso);
            }

            _emitidos.Enqueue(logEvent.RenderMessage());
        }

        public void Dispose() => _liberar.Dispose();
    }
}
