using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Icarus.IntegrationTests.Observability;

public sealed class DiagnosticosEndpointTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task AceptaReportePermitidoSinAutenticacion()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/diagnosticos/frontend", ReporteValido());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RechazaPropiedadesDesconocidasSinReflejarlas()
    {
        using var client = factory.CreateClient();
        var reporte = ReporteValido();
        reporte["mensaje"] = "contenido-que-no-debe-registrarse";

        using var response = await client.PostAsJsonAsync("/diagnosticos/frontend", reporte);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("contenido-que-no-debe-registrarse", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RechazaCuerpoMayorADieciseisKibibytes()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $"{{\"errorId\":\"ERR-0123456789AB\",\"relleno\":\"{new string('a', 17000)}\"}}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/diagnosticos/frontend", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static Dictionary<string, object?> ReporteValido() =>
        new()
        {
            ["errorId"] = "ERR-0123456789AB",
            ["eventName"] = "window.unexpected",
            ["category"] = "unexpected",
            ["source"] = "window",
            ["release"] = "development",
        };
}
