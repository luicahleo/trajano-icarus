using Icarus.Host.Observability;

namespace Icarus.UnitTests.Observability;

public sealed class ReporteDiagnosticoFrontendTests
{
    [Fact]
    public void ContratoSoloExponeCamposSeguros()
    {
        var propiedades = typeof(ReporteDiagnosticoFrontend)
            .GetProperties()
            .Select(p => p.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "Asset", "Category", "ColumnNumber", "CorrelationId", "ErrorId", "EventName",
                "FlowEvents", "LineNumber", "Release", "SessionId", "Source", "StatusCode", "TraceId",
            ],
            propiedades);
    }

    [Fact]
    public void ValidadorAceptaReporteYFlujoPermitidos()
    {
        var reporte = ReporteValido() with
        {
            SessionId = "SES-0123456789AB",
            CorrelationId = "20cc2ea2-2f71-45bb-a667-25f1700431bb",
            TraceId = "0123456789abcdef0123456789abcdef",
            StatusCode = 503,
            Asset = "index-A1b2.js",
            LineNumber = 42,
            ColumnNumber = 7,
            FlowEvents =
            [
                new EventoFlujoCliente(1, new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
                    "flow.navigation", "/clientes/:id", null, null, null, null),
                new EventoFlujoCliente(2, new DateTimeOffset(2026, 8, 16, 10, 0, 1, TimeSpan.Zero),
                    "flow.api_call", "GET /api/clientes/:id", "20cc2ea2-2f71-45bb-a667-25f1700431bb",
                    "0123456789abcdef0123456789abcdef", 200, 25.5),
            ],
        };

        Assert.True(ValidadorReporteDiagnosticoFrontend.EsValido(reporte));
    }

    [Theory]
    [MemberData(nameof(ReportesInvalidos))]
    public void ValidadorRechazaContenidoFueraDeWhitelist(ReporteDiagnosticoFrontend reporte) =>
        Assert.False(ValidadorReporteDiagnosticoFrontend.EsValido(reporte));

    public static TheoryData<ReporteDiagnosticoFrontend> ReportesInvalidos() =>
        new()
        {
            ReporteValido() with { ErrorId = "ERR-invalido" },
            ReporteValido() with { EventName = "usuario.contenido" },
            ReporteValido() with { Category = "mensaje" },
            ReporteValido() with { Source = "formulario" },
            ReporteValido() with { StatusCode = 409 },
            ReporteValido() with { Release = "version con espacios" },
            ReporteValido() with { Asset = "https://sitio/archivo.js?token=secreto" },
            ReporteValido() with
            {
                FlowEvents =
                [
                    new EventoFlujoCliente(1, DateTimeOffset.UtcNow, "flow.navigation",
                        "/clientes/:id?documento=secreto", null, null, null, null),
                ],
            },
            ReporteValido() with
            {
                FlowEvents = Enumerable.Range(1, 51)
                    .Select(i => new EventoFlujoCliente(i, DateTimeOffset.UtcNow,
                        "flow.navigation", "/inicio", null, null, null, null))
                    .ToArray(),
            },
        };

    private static ReporteDiagnosticoFrontend ReporteValido() =>
        new(
            "ERR-0123456789AB",
            "window.unexpected",
            "unexpected",
            "window",
            null,
            null,
            null,
            null,
            "development",
            null,
            null,
            null,
            null);
}
