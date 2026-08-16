using Icarus.BuildingBlocks.Observability;

namespace Icarus.UnitTests.Observability;

public sealed class ReleaseDiagnosticoTests
{
    [Fact]
    public void ResolverUsaLoIndicadoPorLaVariable()
    {
        Assert.Equal("v1.2.3", ReleaseDiagnostico.Resolver("v1.2.3"));
    }

    [Fact]
    public void ResolverCaeADesarrolloSinVariableNiVersion()
    {
        Assert.Equal("development", ReleaseDiagnostico.Sanitizar(null));
    }

    [Theory]
    [InlineData("../secreto", "..secreto")]
    [InlineData("v 1.2+sha", "v1.2sha")]
    [InlineData("", "development")]
    [InlineData("0123456789012345678901234567890123456789ABC", "0123456789012345678901234567890123456789")]
    public void SanitizarConservaSoloCaracteresSegurosYAcota(string? entrada, string esperado) =>
        Assert.Equal(esperado, ReleaseDiagnostico.Sanitizar(entrada));
}
