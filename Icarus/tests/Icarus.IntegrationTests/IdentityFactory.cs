using Icarus.IntegrationTests.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Testcontainers.MsSql;
using Xunit;

namespace Icarus.IntegrationTests;

// Un contenedor SQL Server compartido por toda la colección de integración:
// la app arranca en entorno Testing contra el SQL Server efímero, donde Program
// migra y siembra con las claves fijas de abajo. La imagen se pasa explícita
// (el ctor sin parámetros es obsoleto en 4.13.0) y coincide con la de
// docker-compose.dev.yml: mismo motor en dev, tests y producción.
public sealed class IdentityFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Claves fijas de Testing (la firma del access JWT y su validación Bearer
    // usan exactamente la misma). Nunca son credenciales reales.
    public const string JwtClaveDePrueba = "clave-de-prueba-para-tests-de-integracion-32b+";
    public const string ContrasenaDePrueba = "Semilla-Testing-1234";

    private readonly MsSqlContainer _contenedor =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // Sink compartido para inspeccionar los eventos reales de Serilog en las
    // pruebas de observabilidad. La suite de integración es secuencial.
    public static ColectorSerilog Colector { get; } = new();

    // Cadena del MISMO contenedor compartido: las pruebas que necesitan una
    // base aislada (para no contaminar la base que comparte la suite) crean
    // otra base en este servidor en vez de levantar un SQL Server propio, que
    // es justo lo que IntegracionCollection existe para evitar.
    public string CadenaConexion => _contenedor.GetConnectionString();

    public async Task InitializeAsync() => await _contenedor.StartAsync();

    // El WebApplicationFactory base expone DisposeAsync() -> ValueTask (de
    // IAsyncDisposable) e IAsyncLifetime de xUnit exige DisposeAsync() -> Task:
    // este método "new" es el que xUnit invoca; primero se libera el host y
    // después el contenedor.
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _contenedor.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Icarus", _contenedor.GetConnectionString());
        builder.UseSetting("Jwt:Clave", JwtClaveDePrueba);
        builder.UseSetting("Semilla:ContrasenaPrueba", ContrasenaDePrueba);
        // Recompone el logger real de la aplicación y añade el sink de prueba.
        // ConfigureTestServices corre después de Program.cs: esta registración
        // reemplaza la del host y conserva la configuración declarativa.
        builder.ConfigureTestServices(servicios => servicios.AddSerilog(
            (proveedor, configuracion) => configuracion
                .ReadFrom.Configuration(proveedor.GetRequiredService<IConfiguration>())
                .ReadFrom.Services(proveedor)
                .WriteTo.Sink(Colector),
            preserveStaticLogger: true));
    }
}
