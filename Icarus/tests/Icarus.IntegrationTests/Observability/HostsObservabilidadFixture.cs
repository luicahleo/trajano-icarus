extern alias mvc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Icarus.IntegrationTests.Observability;

/// <summary>Levanta los dos hosts reales (API y MVC) con sus entrypoints y
/// Kestrel en puerto efímero, compartiendo el contenedor SQL de integración con
/// una base exclusiva del escenario. Cada host conserva su propio content root
/// (appsettings con los mismos nombres) y su propio sink de captura, para no
/// mezclar eventos (plan del cierre, tarea 4).</summary>
public sealed class HostsObservabilidadFixture : IAsyncLifetime
{
    private readonly string _cadenaBase;
    private ApiHostFactory? _api;
    private MvcHostFactory? _mvc;

    public HostsObservabilidadFixture(string cadenaBase) => _cadenaBase = cadenaBase;

    public ColectorSerilog ColectorApi { get; } = new();

    public ColectorSerilog ColectorMvc { get; } = new();

    public HttpClient Api { get; private set; } = null!;

    public HttpClient Mvc { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var cadena = ConBaseExclusiva(_cadenaBase, $"IcarusObservabilidad_{Guid.NewGuid():N}");
        _api = new ApiHostFactory(cadena, ColectorApi);
        Api = _api.CreateClient();

        var baseApi = new Uri(Api.BaseAddress!, "api");
        _mvc = new MvcHostFactory(baseApi.ToString(), ColectorMvc);
        // Inicia el host para conocer su puerto efímero; CreateClient(options)
        // no aplica el ajuste automático de Kestrel, así que se pasa explícito.
        _ = _mvc.Services;
        Mvc = _mvc.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = _mvc.ClientOptions.BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Mvc?.Dispose();
        if (_mvc is not null)
            await _mvc.DisposeAsync();
        Api?.Dispose();
        if (_api is not null)
            await _api.DisposeAsync();
    }

    private static string ConBaseExclusiva(string cadena, string nombre)
    {
        var constructor = new SqlConnectionStringBuilder(cadena) { InitialCatalog = nombre };
        return constructor.ConnectionString;
    }

    private sealed class ApiHostFactory : WebApplicationFactory<Program>
    {
        private readonly string _cadena;
        private readonly ColectorSerilog _colector;

        public ApiHostFactory(string cadena, ColectorSerilog colector)
        {
            _cadena = cadena;
            _colector = colector;
            UseKestrel(0);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Icarus", _cadena);
            builder.UseSetting("Jwt:Clave", IdentityFactory.JwtClaveDePrueba);
            builder.UseSetting("Semilla:ContrasenaPrueba", IdentityFactory.ContrasenaDePrueba);
            builder.ConfigureAppConfiguration((_, configuracion) =>
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:WriteTo:Seq:Args:serverUrl"] = "http://127.0.0.1:1",
                }));
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton<IStartupFilter>(new ForzarUn401StartupFilter());
                servicios.AddSerilog((proveedor, configuracion) => configuracion
                    .ReadFrom.Configuration(proveedor.GetRequiredService<IConfiguration>())
                    .ReadFrom.Services(proveedor)
                    .WriteTo.Sink(_colector), preserveStaticLogger: true);
            });
        }
    }

    private sealed class MvcHostFactory : WebApplicationFactory<mvc::Program>
    {
        private readonly string _baseApi;
        private readonly ColectorSerilog _colector;

        public MvcHostFactory(string baseApi, ColectorSerilog colector)
        {
            _baseApi = baseApi;
            _colector = colector;
            UseKestrel(0);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ApiIcarus:BaseUrl", _baseApi);
            builder.ConfigureAppConfiguration((_, configuracion) =>
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:WriteTo:Seq:Args:serverUrl"] = "http://127.0.0.1:1",
                }));
            builder.ConfigureTestServices(servicios => servicios.AddSerilog(
                (proveedor, configuracion) => configuracion
                    .ReadFrom.Configuration(proveedor.GetRequiredService<IConfiguration>())
                    .ReadFrom.Services(proveedor)
                    .WriteTo.Sink(_colector),
                preserveStaticLogger: true));
        }
    }
}

/// <summary>Inyección exclusiva de pruebas sobre el servidor API: al activarse,
/// la siguiente petición a /api/precios-alimentos responde 401 para ejercitar
/// el ciclo 401 → renovación → reintento del cliente MVC real.</summary>
public sealed class ForzarUn401StartupFilter : IStartupFilter
{
    private static int _restantes;

    public static void Activar() => Interlocked.Exchange(ref _restantes, 1);

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (contexto, siguiente) =>
            {
                if (contexto.Request.Path.StartsWithSegments("/api/precios-alimentos")
                    && Interlocked.Decrement(ref _restantes) >= 0)
                {
                    contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await siguiente(contexto);
            });
            next(app);
        };
}
