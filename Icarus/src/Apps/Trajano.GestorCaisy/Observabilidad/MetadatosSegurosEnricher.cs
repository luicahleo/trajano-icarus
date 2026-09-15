using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Copia local y mínima del enriquecedor del backend: GestorCaisy no
/// referencia los building blocks (arrastrarían EF y el dominio). Resuelve el
/// entorno real y normaliza Release con el mismo contrato 1-40 ASCII seguros.</summary>
public sealed class MetadatosSegurosEnricher : ILogEventEnricher
{
    private readonly string _entorno;
    private readonly string _release;

    public MetadatosSegurosEnricher(IConfiguration configuracion)
    {
        _entorno = ResolverEntorno(configuracion);
        _release = Sanitizar(
            configuracion["Serilog:Properties:Release"]
            ?? configuracion["ICARUS_RELEASE"]);
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Entorno", _entorno));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Release", _release));
    }

    private static string ResolverEntorno(IConfiguration configuracion)
    {
        var valor = configuracion["ASPNETCORE_ENVIRONMENT"]
            ?? configuracion["DOTNET_ENVIRONMENT"];
        return string.IsNullOrWhiteSpace(valor) ? "Production" : valor;
    }

    private static string Sanitizar(string? valor)
    {
        var limpio = new string((valor ?? "development")
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            .Take(40)
            .ToArray());
        return limpio.Length == 0 ? "development" : limpio;
    }
}

public static class MetadatosSegurosExtensions
{
    public static LoggerConfiguration WithMetadatosSeguros(
        this LoggerEnrichmentConfiguration enrichers, IConfiguration configuracion) =>
        enrichers.With(new MetadatosSegurosEnricher(configuracion));
}
