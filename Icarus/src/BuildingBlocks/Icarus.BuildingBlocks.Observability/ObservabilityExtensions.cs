using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Icarus.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    /// <summary>Sinks, niveles, filtros y enrichers viven en la sección
    /// <c>Serilog</c> de la configuración (spec de Serilog y Seq). El código
    /// solo compone esa configuración y habilita la resolución de servicios.</summary>
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services));

        return builder;
    }
}
