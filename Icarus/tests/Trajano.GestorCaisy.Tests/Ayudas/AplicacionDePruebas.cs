using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Tests.Ayudas;

public sealed partial class AplicacionDePruebas : WebApplicationFactory<Program>
{
    public ApiIcarusFalsa Api { get; } = new();

    // Credenciales que la API falsa acepta como válidas.
    public const string CorreoValido = "gestor@caisy.test";
    public const string ClaveValida = "Clave-De-Prueba-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // ConfigureTestServices corre DESPUÉS de Program.cs: reemplaza de
        // verdad el cliente tipado registrado por la aplicación.
        builder.ConfigureTestServices(services =>
        {
            // Última registración gana: sin RemoveAll, que en el flujo del
            // host diferido llega a borrar la registración de la aplicación.
            services.AddSingleton<IApiIcarusClient>(Api);
        });
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
