using System.Security.Cryptography;

namespace Icarus.BuildingBlocks.Observability;

public static class DiagnosticIds
{
    public const string SessionHeader = "X-Session-Id";
    public const string TraceHeader = "X-Trace-Id";

    public static string NuevoErrorId() => $"ERR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";

    public static bool EsErrorId(string? valor) =>
        EsIdentificadorHexadecimal(valor, "ERR-");

    public static bool EsSessionId(string? valor) =>
        EsIdentificadorHexadecimal(valor, "SES-");

    private static bool EsIdentificadorHexadecimal(string? valor, string prefijo) =>
        valor is not null
        && valor.Length == 16
        && valor.StartsWith(prefijo, StringComparison.Ordinal)
        && valor.AsSpan(4).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
