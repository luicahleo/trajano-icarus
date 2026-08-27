using System.Net;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

[Collection(IntegracionCollection.Nombre)]
public sealed class ProduccionMortalidadEndpointsTests(IdentityFactory factory)
{
    [Fact]
    public async Task ProduccionSinTokenDevuelve401()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/api/galpones/{Guid.NewGuid()}/produccion");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task MortalidadSinTokenDevuelve401()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/api/galpones/{Guid.NewGuid()}/mortalidad");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
