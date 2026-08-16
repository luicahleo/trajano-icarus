using Microsoft.AspNetCore.Http;

namespace Icarus.BuildingBlocks.Observability;

public static class DiagnosticContext
{
    private const string ErrorIdKey = "Icarus.Diagnostic.ErrorId";
    private const string TraceIdKey = "Icarus.Diagnostic.TraceId";

    public static void EstablecerErrorId(HttpContext contexto, string errorId) =>
        contexto.Items[ErrorIdKey] = errorId;

    public static string? ObtenerErrorId(HttpContext contexto) =>
        contexto.Items[ErrorIdKey] as string;

    public static void EstablecerTraceId(HttpContext contexto, string traceId) =>
        contexto.Items[TraceIdKey] = traceId;

    public static string? ObtenerTraceId(HttpContext contexto) =>
        contexto.Items[TraceIdKey] as string;
}
