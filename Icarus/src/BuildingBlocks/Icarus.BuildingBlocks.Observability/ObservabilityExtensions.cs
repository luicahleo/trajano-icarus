using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace Icarus.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Aplicacion", "Icarus")
            .Enrich.WithProperty("Entorno", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console(new CompactJsonFormatter()));

        return builder;
    }
}
