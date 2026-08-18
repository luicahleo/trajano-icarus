using System.Net;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

public sealed class ProduccionMortalidadEndpointsTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task ProduccionSinTokenDevuelve401()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/galpones/{Guid.NewGuid()}/produccion");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task MortalidadSinTokenDevuelve401()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/galpones/{Guid.NewGuid()}/mortalidad");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
