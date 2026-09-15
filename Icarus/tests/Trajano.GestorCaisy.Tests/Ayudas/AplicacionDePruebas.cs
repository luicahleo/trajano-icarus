using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Observabilidad;

namespace Trajano.GestorCaisy.Tests.Ayudas;

public sealed partial class AplicacionDePruebas : WebApplicationFactory<Program>
{
    public ApiIcarusFalsa Api { get; } = new();

    // Sink por instancia: los eventos de este host no se mezclan con otros.
    public ColectorSerilog Colector { get; } = new();

    // Destino Seq adicional para las pruebas de ingestión aisladas.
    public string? UrlSeqAdicional { get; set; }

    // Entorno real que resuelve la aplicación; permite cubrir Development,
    // Testing y Production con la misma política de privacidad.
    public string Entorno { get; set; } = "Testing";

    // Con la API real se conserva el cliente tipado y el transporte HttpClient
    // productivo; el fake solo se registra cuando esta bandera es falsa.
    public bool UsarApiReal { get; set; }

    // URL absoluta de la API para escenarios que no deben tocar red real.
    public string? BaseUrlApi { get; set; }

    // Credenciales que la API falsa acepta como válidas.
    public const string CorreoValido = "gestor@caisy.test";
    public const string ClaveValida = "Clave-De-Prueba-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Entorno);
        // Aísla la URL Seq de desarrollo y fija la URL de la API cuando el
        // escenario lo requiere.
        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            var fuentes = new Dictionary<string, string?>();
            // El override de Seq solo aplica cuando el JSON de desarrollo
            // declara ese sink; en otros entornos la clave huérfana molesta.
            if (Entorno == "Development")
                fuentes["Serilog:WriteTo:Seq:Args:serverUrl"] = "http://127.0.0.1:1";
            if (!string.IsNullOrWhiteSpace(BaseUrlApi))
                fuentes["ApiIcarus:BaseUrl"] = BaseUrlApi;
            if (fuentes.Count > 0)
                configuracion.AddInMemoryCollection(fuentes);
        });
        // ConfigureTestServices corre DESPUÉS de Program.cs: reemplaza de
        // verdad el cliente tipado registrado por la aplicación.
        builder.ConfigureTestServices(services =>
        {
            // Última registración gana: sin RemoveAll, que en el flujo del
            // host diferido llega a borrar la registración de la aplicación.
            if (!UsarApiReal)
                services.AddSingleton<IApiIcarusClient>(Api);
            // Controladores y filtros exclusivos de las pruebas.
            services.AddControllers().AddApplicationPart(
                typeof(ControladorErroresDePrueba).Assembly);
            services.Configure<MvcOptions>(
                opciones => opciones.Filters.Add<FiltroFalloPaginaDeErrorPrueba>());
        });
        // Recompone el logger real y añade el sink de prueba.
        builder.ConfigureTestServices(servicios => servicios.AddSerilog(
            (proveedor, configuracion) =>
            {
                configuracion
                    .ReadFrom.Configuration(proveedor.GetRequiredService<IConfiguration>())
                    .ReadFrom.Services(proveedor)
                    .WriteTo.Sink(Colector);
                if (!string.IsNullOrWhiteSpace(UrlSeqAdicional))
                    configuracion.WriteTo.Seq(UrlSeqAdicional,
                        period: TimeSpan.FromMilliseconds(200));
            },
            preserveStaticLogger: true));
    }

    public HttpClient CrearClienteSinRedireccion() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Recorre el flujo real de acceso y entrega un cliente autenticado.
    public async Task<HttpClient> AccederAsync(string rol = "GestorCaisy", int? funcCaisy = 1)
    {
        var cliente = CrearClienteSinRedireccion();
        var token = await TokenAntiforgeryAsync(cliente, "/Sesion/Acceder");
        Api.AlIniciarSesion = (_, _) =>
            new SesionApi(CreadorTokens.Crear(rol, funcCaisy), CreadorTokens.Crear(), 900);
        var respuesta = await cliente.PostAsync("/Sesion/Acceder", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = CorreoValido,
                ["Contrasena"] = ClaveValida,
                ["__RequestVerificationToken"] = token,
            }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    public static async Task<string> TokenAntiforgeryAsync(
        HttpClient cliente, string ruta)
    {
        var html = await cliente.GetStringAsync(ruta);
        return ExtraerTokenAntiforgery(html)
            ?? throw new InvalidOperationException($"La página {ruta} no trae token antiforgery.");
    }

    public static string? ExtraerTokenAntiforgery(string html)
    {
        var coincidencia = TokenRegex().Match(html);
        return coincidencia.Success ? coincidencia.Groups["valor"].Value : null;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<valor>[^\"]+)\"")]
    private static partial Regex TokenRegex();
}
