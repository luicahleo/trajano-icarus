using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Icarus.IntegrationTests.ControlAcceso;

internal sealed class ArgosHttpDoble : IAsyncDisposable
{
    private const string RutaExtracciones = "/api/v2/control-acceso/extracciones";
    private const string RutaIdentificaciones = "/api/v2/control-acceso/identificaciones";
    private readonly WebApplication _aplicacion;
    private readonly ConcurrentDictionary<string, (string Cuerpo, string Autorizacion)> _solicitudes = new();

    private ArgosHttpDoble(WebApplication aplicacion)
    {
        _aplicacion = aplicacion;
    }

    public Uri BaseAddress { get; private set; } = new("http://127.0.0.1/");

    public static async Task<ArgosHttpDoble> IniciarAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(opciones => opciones.Listen(IPAddress.Loopback, 0));
        var aplicacion = builder.Build();
        var doble = new ArgosHttpDoble(aplicacion);

        aplicacion.MapPost(RutaExtracciones, async (HttpRequest request) =>
        {
            await doble.CapturarAsync(RutaExtracciones, request);
            return Results.Json(new
            {
                exitoso = true,
                vector = new[] { 0.125f, -0.25f, 0.5f },
                modelo_formato = "arcface-cosine-512",
                version_modelo = 2,
                pad_aprobado = true,
                codigo = (string?)null,
            });
        });

        aplicacion.MapPost(RutaIdentificaciones, async (HttpRequest request) =>
        {
            await doble.CapturarAsync(RutaIdentificaciones, request);
            return Results.Json(new
            {
                identificado = true,
                trabajador_id = "31b806bc-3ac3-4a0e-a0a1-9d18129f53ca",
                codigo = (string?)null,
            });
        });

        await aplicacion.StartAsync();
        var direcciones = aplicacion.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;
        var direccion = direcciones.Single(d => d.StartsWith("http://", StringComparison.Ordinal));
        doble.BaseAddress = new Uri(direccion);
        return doble;
    }

    public Task<string> CuerpoRecibidoAsync(string ruta) =>
        Task.FromResult(_solicitudes[ruta].Cuerpo);

    public Task<string> AutorizacionRecibidaAsync(string ruta) =>
        Task.FromResult(_solicitudes[ruta].Autorizacion);

    private async Task CapturarAsync(string ruta, HttpRequest request)
    {
        using var lector = new StreamReader(request.Body);
        var cuerpo = await lector.ReadToEndAsync(request.HttpContext.RequestAborted);
        _solicitudes[ruta] = (cuerpo, request.Headers.Authorization.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await _aplicacion.StopAsync();
        await _aplicacion.DisposeAsync();
    }
}
