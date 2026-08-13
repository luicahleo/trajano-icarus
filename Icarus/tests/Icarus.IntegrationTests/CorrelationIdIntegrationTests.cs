using Xunit;

namespace Icarus.IntegrationTests;

public class CorrelationIdIntegrationTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public CorrelationIdIntegrationTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task TodaRespuestaLlevaCorrelationId()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.True(respuesta.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task CorrelationIdEntranteSePropagaALaRespuesta()
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/health");
        pedido.Headers.Add("X-Correlation-ID", "trace-prueba-1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal("trace-prueba-1",
            respuesta.Headers.GetValues("X-Correlation-ID").Single());
    }
}
