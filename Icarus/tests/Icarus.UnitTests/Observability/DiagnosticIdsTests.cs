using Icarus.BuildingBlocks.Observability;

namespace Icarus.UnitTests.Observability;

public sealed class DiagnosticIdsTests
{
    [Fact]
    public void NuevoErrorIdGeneraReferenciasOpacasValidasYDistintas()
    {
        var primero = DiagnosticIds.NuevoErrorId();
        var segundo = DiagnosticIds.NuevoErrorId();

        Assert.Matches("^ERR-[0-9A-F]{12}$", primero);
        Assert.NotEqual(primero, segundo);
    }

    [Theory]
    [InlineData("SES-0123456789AB", true)]
    [InlineData("ses-0123456789ab", false)]
    [InlineData("ERR-0123456789AB", false)]
    [InlineData(null, false)]
    public void SessionIdSoloAceptaElFormatoOpaco(string? valor, bool esperado) =>
        Assert.Equal(esperado, DiagnosticIds.EsSessionId(valor));
}
