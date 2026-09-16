using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Trajano.GestorCaisy.Observabilidad;
using Trajano.GestorCaisy.Tests.Ayudas;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Monitor de la cola asíncrona de consola de GestorCaisy: el host lo
/// conecta al sink declarado en JSON (observable por métricas) y la cola
/// acotada no bloquea al productor aunque el consumidor esté detenido
/// (plan del cierre, tarea 5).</summary>
public sealed class ColaConsolaTests
{
    [Fact]
    public async Task ElHostConectaElMonitorDeLaColaDeConsola()
    {
        using var escucha = new MeterListener();
        var capacidad = new int[1];
        escucha.InstrumentPublished = (instrumento, oyente) =>
        {
            if (instrumento.Meter.Name == MonitorColaConsola.NombreMedidor)
                oyente.EnableMeasurementEvents(instrumento);
        };
        escucha.SetMeasurementEventCallback<int>((instrumento, valor, _, _) =>
        {
            if (instrumento.Name == "cola.consola.capacidad")
                Interlocked.Exchange(ref capacidad[0], valor);
        });
        escucha.Start();

        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();
        await cliente.GetAsync("/Sesion/Acceder");

        // El instrumento se publica al componer el logger del host; se sondea
        // con cota amplia para no depender del orden de arranque.
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (Volatile.Read(ref capacidad[0]) != 10000 && DateTime.UtcNow < limite)
        {
            escucha.RecordObservableInstruments();
            await Task.Delay(50);
        }

        Assert.Equal(10000, Volatile.Read(ref capacidad[0]));
    }

    [Fact]
    public async Task LaColaNoBloqueaAlProductorAunqueElConsumidorEsteDetenido()
    {
        using var consumidor = new SinkBloqueable();
        using var monitor = new MonitorColaConsola();
        using var logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.Sink(consumidor), bufferSize: 4, blockWhenFull: false, monitor: monitor)
            .CreateLogger();

        consumidor.Bloquear();
        logger.Information("evento de calentamiento");
        Assert.True(consumidor.EsperandoConsumidor(TimeSpan.FromSeconds(15)),
            "El consumidor no arrancó.");

        var productor = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
                logger.Information("evento {Indice}", i);
        });

        var termino = await Task.WhenAny(productor, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(productor, termino);

        var inspector = monitor.Inspector!;
        Assert.True(inspector.DroppedMessagesCount > 0);
        consumidor.Liberar();
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
