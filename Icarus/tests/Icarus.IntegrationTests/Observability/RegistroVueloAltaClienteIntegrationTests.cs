namespace Icarus.IntegrationTests;

public sealed class RegistroVueloAltaClienteIntegrationTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public RegistroVueloAltaClienteIntegrationTests(IdentityFactory factory) => _factory = factory;

    [Fact]
    public async Task PeticionDelPilotoExponeTraceIdSinExponerDatosDeLaCuenta()
    {
        using var cliente = _factory.CreateClient();
        using var respuesta = await cliente.GetAsync("/health");
        var traceId = respuesta.Headers.GetValues("X-Trace-Id").Single();

        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.DoesNotContain("email", traceId, StringComparison.OrdinalIgnoreCase);
    }
}
