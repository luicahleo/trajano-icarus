using Xunit;

namespace Icarus.IntegrationTests;

[Collection(IntegracionCollection.Nombre)]
public class HealthEndpointTests
{
    private readonly IdentityFactory _factory;

    public HealthEndpointTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthResponde200()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
    }
}
