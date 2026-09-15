using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Icarus.BuildingBlocks.Observability;

/// <summary>Resuelve el entorno real del host y normaliza el Release declarado
/// en configuración. Se declara desde JSON para que la selección de enrichers
/// viva fuera del código (spec de Serilog y Seq).</summary>
public sealed class MetadatosSegurosEnricher : ILogEventEnricher
{
    private readonly string _entorno;
    private readonly string _release;

    public MetadatosSegurosEnricher(IConfiguration configuracion)
    {
        _entorno = ResolverEntorno(configuracion);
        _release = ReleaseDiagnostico.Resolver(
            configuracion["Serilog:Properties:Release"] ?? configuracion["ICARUS_RELEASE"]);
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
}

public static class MetadatosSegurosExtensions
{
    public static LoggerConfiguration WithMetadatosSeguros(
        this LoggerEnrichmentConfiguration enrichers, IConfiguration configuracion) =>
        enrichers.With(new MetadatosSegurosEnricher(configuracion));
}
