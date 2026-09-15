using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Trajano.GestorCaisy.Tests.Ayudas;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Ingestión real de GestorCaisy en un Seq aislado: el resumen HTTP
/// del host llega con su propiedad Aplicacion, sin tocar el stack actual
/// (plan de Serilog y Seq, tarea 5).</summary>
public sealed class IngestionSeqTests : IAsyncLifetime
{
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
    public async Task ElResumenDeGestorCaisyLlegaASeqAislado()
    {
        using var aplicacion = new AplicacionDePruebas { UrlSeqAdicional = UrlSeq };
        var cliente = aplicacion.CreateClient();

        await cliente.GetAsync("/Sesion/Acceder");

        var crudo = await ConsultarSeqCrudoAsync();
        Assert.Contains("Trajano.GestorCaisy", crudo);
        Assert.Contains("http.request.completed", crudo);
    }

    private async Task<string> ConsultarSeqCrudoAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri(UrlSeq) };
        string? ultimo = null;
        for (var intento = 0; intento < 20; intento++)
        {
            try
            {
                var respuesta = await http.GetAsync("/api/events?count=50");
                ultimo = await respuesta.Content.ReadAsStringAsync();
                if (ultimo.Contains("http.request.completed", StringComparison.Ordinal))
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
}
