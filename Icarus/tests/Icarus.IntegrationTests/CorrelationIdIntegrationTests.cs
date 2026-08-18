using Xunit;

namespace Icarus.IntegrationTests;

[Collection(IntegracionCollection.Nombre)]
public class CorrelationIdIntegrationTests
{
    private readonly IdentityFactory _factory;

    public CorrelationIdIntegrationTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task TodaRespuestaLlevaCorrelationId()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.True(respuesta.Headers.Contains("X-Correlation-ID"));
        Assert.True(respuesta.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task CorrelationIdEntranteSePropagaALaRespuesta()
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/health");
        const string correlationId = "20cc2ea2-2f71-45bb-a667-25f1700431bb";
        pedido.Headers.Add("X-Correlation-ID", correlationId);
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(correlationId,
            respuesta.Headers.GetValues("X-Correlation-ID").Single());
    }
}
