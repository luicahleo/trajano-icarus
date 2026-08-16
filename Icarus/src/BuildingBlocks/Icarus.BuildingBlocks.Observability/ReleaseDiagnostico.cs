using System.Reflection;

namespace Icarus.BuildingBlocks.Observability;

/// <summary>Release de la aplicación para logs y reportes, sin caracteres
/// inseguros ni PII (spec: Release 1-40 ASCII seguros, fallback development).</summary>
public static class ReleaseDiagnostico
{
    public static string Resolver(string? variable) =>
        Sanitizar(string.IsNullOrWhiteSpace(variable) ? VersionEnsamblado() : variable);

    public static string Sanitizar(string? valor)
    {
        var limpio = new string((valor ?? "development")
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            .Take(40)
            .ToArray());
        return limpio.Length == 0 ? "development" : limpio;
    }

    private static string VersionEnsamblado() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "development";
}
