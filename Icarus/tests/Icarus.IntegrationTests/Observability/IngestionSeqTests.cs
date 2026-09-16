using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Ingestión real y fallos de transporte de Seq en infraestructura
/// aislada: contenedor propio, datos sintéticos y sin tocar el stack actual.
/// No se promete entrega exactamente una vez ni supervivencia a terminación
/// abrupta (plan de Serilog y Seq, tarea 5).</summary>
public sealed class IngestionSeqTests : IAsyncLifetime
{
    private const string Evento = "prueba.ingesta";
    private readonly IContainer _seq = new ContainerBuilder("datalust/seq:2026.1")
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("SEQ_FIRSTRUN_NOAUTHENTICATION", "true")
        .WithPortBinding(0, 80)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            peticion => peticion.ForPort(80).ForPath("/api")))
        .Build();

    public async Task InitializeAsync() => await _seq.StartAsync();

    public async Task DisposeAsync() => await _seq.DisposeAsync();

    private string UrlSeq => $"http://{_seq.Hostname}:{_seq.GetMappedPublicPort(80)}";

    [Fact]
    public async Task UnEventoEstructuradoLlegaYSeConsultaEnSeq()
    {
        var capturador = new CapturadorSink();
        await using var logger = new LoggerConfiguration()
            .Enrich.WithProperty("Aplicacion", "IcarusPrueba")
            .WriteTo.Seq(UrlSeq, batchPostingLimit: 10,
                period: TimeSpan.FromMilliseconds(200), queueSizeLimit: 1000)
            .WriteTo.Sink(capturador)
            .CreateLogger();

        logger.Information("{EventName}: {Operation} en fase {Phase}", Evento, "prueba", "inicio");
        await logger.DisposeAsync();

        Assert.Single(capturador.Eventos);
        var crudo = await ConsultarSeqCrudoAsync();
        Assert.Contains(Evento, crudo);
    }

    [Fact]
    public async Task SinConfiguracionRemotaLaConsolaSigueRecibiendo()
    {
        var capturador = new CapturadorSink();
        await using var logger = new LoggerConfiguration()
            .WriteTo.Sink(capturador)
            .CreateLogger();

        logger.Information("{EventName}: sin Seq", Evento);

        Assert.Single(capturador.Eventos);
    }

    [Fact]
    public async Task UnDestinoInaccesibleNoRompeNiBloqueaElRegistro()
    {
        var capturador = new CapturadorSink();
        await using var logger = new LoggerConfiguration()
            .WriteTo.Seq("http://127.0.0.1:1", batchPostingLimit: 10,
                period: TimeSpan.FromMilliseconds(200), queueSizeLimit: 1000)
            .WriteTo.Sink(capturador)
            .CreateLogger();

        var inicio = DateTime.UtcNow;
        logger.Information("{EventName}: destino caído", Evento);
        var transcurrido = DateTime.UtcNow - inicio;

        // El registro es síncrono y no espera al transporte; cota amplia para no
        // depender de tiempos frágiles.
        Assert.True(transcurrido < TimeSpan.FromSeconds(2), $"El registro tardó {transcurrido}.");
        Assert.Single(capturador.Eventos);
    }

    [Fact]
    public async Task UnRechazoDeApiKeySinteticaNoRompeElRegistro()
    {
        using var servidor = new Servidor401();
        var capturador = new CapturadorSink();
        await using var logger = new LoggerConfiguration()
            .WriteTo.Seq($"http://127.0.0.1:{servidor.Puerto}",
                apiKey: "clave-sintetica-rechazada",
                batchPostingLimit: 10, period: TimeSpan.FromMilliseconds(200),
                queueSizeLimit: 1000)
            .WriteTo.Sink(capturador)
            .CreateLogger();

        logger.Information("{EventName}: rechazo de clave", Evento);
        await logger.DisposeAsync();

        Assert.Single(capturador.Eventos);
    }

    [Fact]
    public async Task SeqRecibeEventosAunqueLaConsolaEsteBloqueada()
    {
        using var consumidor = new SinkBloqueable();
        await using var logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.Sink(consumidor), bufferSize: 2, blockWhenFull: false)
            .WriteTo.Seq(UrlSeq, batchPostingLimit: 10,
                period: TimeSpan.FromMilliseconds(200), queueSizeLimit: 1000)
            .CreateLogger();

        consumidor.Bloquear();
        logger.Information("{EventName}: consola bloqueada", Evento);

        // Seq es independiente del consumidor de consola bloqueado; esta prueba
        // no dice nada sobre la cola interna de Seq.
        var crudo = await ConsultarSeqCrudoAsync();
        Assert.Contains(Evento, crudo);

        consumidor.Liberar();
    }

    private async Task<string> ConsultarSeqCrudoAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri(UrlSeq) };
        string? ultimo = null;
        for (var intento = 0; intento < 20; intento++)
        {
            try
            {
                var respuesta = await http.GetAsync("/api/events?count=20");
                ultimo = await respuesta.Content.ReadAsStringAsync();
                if (ultimo.Contains(Evento, StringComparison.Ordinal))
                    return ultimo;
            }
            catch (HttpRequestException)
            {
                // Seq aún no acepta consultas: se reintenta.
            }
            await Task.Delay(250);
        }
        return ultimo ?? string.Empty;
    }

    private sealed class SinkBloqueable : ILogEventSink, IDisposable
    {
        private readonly ManualResetEventSlim _liberar = new(true);

        public void Bloquear() => _liberar.Reset();

        public void Liberar() => _liberar.Set();

        public void Emit(LogEvent logEvent) => _liberar.Wait(TimeSpan.FromSeconds(30));

        public void Dispose() => _liberar.Dispose();
    }

    private sealed class CapturadorSink : ILogEventSink
    {
        private readonly List<LogEvent> _eventos = [];
        public IReadOnlyList<LogEvent> Eventos => _eventos;
        public void Emit(LogEvent logEvent)
        {
            lock (_eventos) _eventos.Add(logEvent);
        }
    }

    // Destino controlado que rechaza la ingestión con 401, para simular una
    // API key rechazada sin depender del stack real.
    private sealed class Servidor401 : IDisposable
    {
        private readonly TcpListener _escucha;

        public Servidor401()
        {
            _escucha = new TcpListener(IPAddress.Loopback, 0);
            _escucha.Start();
            Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;
            _ = Task.Run(AtenderAsync);
        }

        public int Puerto { get; }

        private async Task AtenderAsync()
        {
            while (true)
            {
                TcpClient cliente;
                try
                {
                    cliente = await _escucha.AcceptTcpClientAsync();
                }
                catch (SocketException)
                {
                    return;
                }
                _ = Task.Run(async () =>
                {
                    using var conexion = cliente;
                    using var flujo = conexion.GetStream();
                    var buffer = new byte[4096];
                    try
                    {
                        _ = await flujo.ReadAsync(buffer);
                        await flujo.WriteAsync(Encoding.ASCII.GetBytes(
                            "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
                    }
                    catch (IOException)
                    {
                        // Conexión cerrada por el cliente: nada que responder.
                    }
                });
            }
        }

        public void Dispose() => _escucha.Stop();
    }
}
