using System.Text.Json.Serialization;
using Icarus.BuildingBlocks.Observability;

namespace Icarus.Host.Observability;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReporteDiagnosticoFrontend(
    string ErrorId,
    string EventName,
    string Category,
    string Source,
    string? SessionId,
    string? CorrelationId,
    string? TraceId,
    int? StatusCode,
    string? Release,
    string? Asset,
    int? LineNumber,
    int? ColumnNumber,
    IReadOnlyList<EventoFlujoCliente>? FlowEvents);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EventoFlujoCliente(
    int Seq,
    DateTimeOffset Timestamp,
    string EventName,
    string Detail,
    string? CorrelationId,
    string? TraceId,
    int? StatusCode,
    double? DurationMs);

public static class ValidadorReporteDiagnosticoFrontend
{
    public const int MaximoEventosFlujo = 50;

    public static bool EsValido(ReporteDiagnosticoFrontend reporte) =>
        DiagnosticIds.EsErrorId(reporte.ErrorId)
        && EsEvento(reporte.EventName)
        && EsCategoria(reporte.Category)
        && EsFuente(reporte.Source)
        && (reporte.SessionId is null || DiagnosticIds.EsSessionId(reporte.SessionId))
        && EsCorrelationId(reporte.CorrelationId)
        && EsTraceId(reporte.TraceId)
        && reporte.StatusCode is null or >= 500 and <= 599
        && EsRelease(reporte.Release)
        && EsAsset(reporte.Asset)
        && EsPosicion(reporte.LineNumber)
        && EsPosicion(reporte.ColumnNumber)
        && EsFlujo(reporte.FlowEvents);

    private static bool EsEvento(string? valor) => valor is
        "router.unexpected"
        or "window.unexpected"
        or "promise.unhandled"
        or "http.network_failed"
        or "http.server_failed"
        or "chunk.load_failed";

    private static bool EsCategoria(string? valor) => valor is
        "unexpected" or "network" or "server" or "chunk";

    private static bool EsFuente(string? valor) => valor is
        "router" or "window" or "promise" or "http";

    private static bool EsCorrelationId(string? valor) =>
        valor is null || Guid.TryParse(valor, out _);

    private static bool EsTraceId(string? valor) =>
        valor is null
        || valor.Length == 32
        && valor.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool EsRelease(string? valor) =>
        valor is null
        || valor.Length is >= 1 and <= 40
        && valor.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool EsAsset(string? valor) =>
        valor is null
        || valor.Length is >= 1 and <= 120
        && valor.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool EsPosicion(int? valor) => valor is null or >= 1 and <= 10_000_000;

    private static bool EsFlujo(IReadOnlyList<EventoFlujoCliente>? eventos) =>
        eventos is null
        || eventos.Count <= MaximoEventosFlujo && eventos.All(EsEventoFlujo);

    private static bool EsEventoFlujo(EventoFlujoCliente evento) =>
        evento.Seq is >= 1 and <= 10_000
        && evento.Timestamp.Offset == TimeSpan.Zero
        && EsDetalle(evento.EventName, evento.Detail)
        && EsCorrelationId(evento.CorrelationId)
        && EsTraceId(evento.TraceId)
        && evento.StatusCode is null or >= 100 and <= 599
        && evento.DurationMs is null or >= 0 and <= 600_000;

    private static bool EsDetalle(string? evento, string? detalle)
    {
        if (detalle is null || detalle.Length is < 1 or > 120 || detalle.Any(c => !EsCaracterRuta(c)))
            return false;

        if (evento == "flow.navigation") return detalle.StartsWith("/", StringComparison.Ordinal);
        if (evento != "flow.api_call") return false;

        var separador = detalle.IndexOf(' ');
        if (separador < 1 || separador == detalle.Length - 1) return false;
        var metodo = detalle[..separador];
        var ruta = detalle[(separador + 1)..];
        return metodo is "GET" or "POST" or "PUT" or "DELETE"
            && ruta.StartsWith("/api/", StringComparison.Ordinal);
    }

    private static bool EsCaracterRuta(char caracter) =>
        char.IsAsciiLetterOrDigit(caracter) || caracter is '/' or ':' or '.' or '_' or '-' or ' ';
}
