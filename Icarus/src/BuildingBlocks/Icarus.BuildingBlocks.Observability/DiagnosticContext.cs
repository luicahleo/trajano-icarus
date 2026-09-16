using Microsoft.AspNetCore.Http;

namespace Icarus.BuildingBlocks.Observability;

public static class DiagnosticContext
{
    private const string ErrorIdKey = "Icarus.Diagnostic.ErrorId";
    private const string TraceIdKey = "Icarus.Diagnostic.TraceId";
    private const string ClienteIdKey = "Icarus.Diagnostic.ClienteId";
    private const string RolKey = "Icarus.Diagnostic.Rol";

    public static void EstablecerErrorId(HttpContext contexto, string errorId) =>
        contexto.Items[ErrorIdKey] = errorId;

    public static string? ObtenerErrorId(HttpContext contexto) =>
        contexto.Items[ErrorIdKey] as string;

    public static void EstablecerTraceId(HttpContext contexto, string traceId) =>
        contexto.Items[TraceIdKey] = traceId;

    public static string? ObtenerTraceId(HttpContext contexto) =>
        contexto.Items[TraceIdKey] as string;

    // Identidad observacional permitida: tenant opaco y rol validado. Se guarda
    // en Items para que siga disponible cuando los scopes internos ya se
    // desenrollaron (por ejemplo, el log de error exterior).
    public static void EstablecerClienteId(HttpContext contexto, Guid clienteId) =>
        contexto.Items[ClienteIdKey] = clienteId;

    public static Guid? ObtenerClienteId(HttpContext contexto) =>
        contexto.Items[ClienteIdKey] as Guid?;

    public static void EstablecerRol(HttpContext contexto, string rol) =>
        contexto.Items[RolKey] = rol;

    public static string? ObtenerRol(HttpContext contexto) =>
        contexto.Items[RolKey] as string;
}
