using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace Icarus.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Aplicacion", "Icarus")
                .Enrich.WithProperty("Entorno", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithProperty("Release", ReleaseDiagnostico.Resolver(context.Configuration["ICARUS_RELEASE"]))
                .WriteTo.Console(new CompactJsonFormatter());

            // Seq es opcional: la consola JSON sigue siendo el fallback. Una
            // caída de Seq no impide responder peticiones (spec).
            var seqUrl = context.Configuration["Seq:Url"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                var apiKey = context.Configuration["Seq:ApiKey"];
                config.WriteTo.Seq(
                    seqUrl,
                    apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
            }
        });

        return builder;
    }
}
