using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Icarus.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HealthResponde200()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
    }
}
