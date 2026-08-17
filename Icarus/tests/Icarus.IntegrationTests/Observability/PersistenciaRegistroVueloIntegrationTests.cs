namespace Icarus.IntegrationTests;

public sealed class PersistenciaRegistroVueloIntegrationTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public PersistenciaRegistroVueloIntegrationTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task PeticionRealConservaTraceIdParaLaNarracionDeLaPeticion()
    {
        using var cliente = _factory.CreateClient();
        using var respuesta = await cliente.GetAsync("/health");

        Assert.True(respuesta.Headers.TryGetValues("X-Trace-Id", out var valores));
        Assert.Matches("^[0-9a-f]{32}$", valores!.Single());
    }
}
