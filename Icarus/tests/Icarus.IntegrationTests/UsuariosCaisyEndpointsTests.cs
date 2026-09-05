using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// SP8A Tarea 1 (spec: "Aplicaciones y autorización"): la administración de
// cuentas CAISY es del Administrador de plataforma; las cuentas creadas son
// globales (sin tenant) y solo acceden con su funcionalidad asignada. Una
// cuenta desactivada no renueva sesión.
[Collection(IntegracionCollection.Nombre)]
public class UsuariosCaisyEndpointsTests
{
    private readonly IdentityFactory _factory;

    public UsuariosCaisyEndpointsTests(IdentityFactory factory) => _factory = factory;

    private static string ContrasenaSemilla => IdentityFactory.ContrasenaDePrueba;

    private async Task<string> LoginComo(string email, string contrasena)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Autenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private static HttpRequestMessage AutenticadoConContenido(HttpMethod metodo, string url, string token, object cuerpo) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(cuerpo),
        };

    [Fact]
    public async Task CrearCuentaCaisySinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios-caisy/",
            new { email = "caisy-nuevo@icarus.test", contrasena = "Clave-Caisy-123", funcionalidades = new[] { "GestorPedidoAlimento" } });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteNoPuedeAdministrarCuentasCaisy()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente, ContrasenaSemilla);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(AutenticadoConContenido(HttpMethod.Post, "/api/usuarios-caisy/", token,
            new
            {
                email = "caisy-prohibido@icarus.test",
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            }));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task AdminCreaCuentaCaisyYLaCuentaUsaSuFuncionAsignada()
    {
        var tokenAdmin = await LoginComo(SemillaIdentidad.EmailAdmin, ContrasenaSemilla);
        var emailCaisy = $"caisy-{Guid.NewGuid():N}@icarus.test";
        var cliente = _factory.CreateClient();

        var alta = await cliente.SendAsync(AutenticadoConContenido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var tokenCaisy = await LoginComo(emailCaisy, "Clave-Caisy-123");
        var me = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/identidad/me", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var cuerpoMe = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GestorCaisy", cuerpoMe.GetProperty("rol").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpoMe.GetProperty("clienteId").ValueKind);

        var sondeo = await cliente.SendAsync(Autenticado(
            HttpMethod.Get, "/api/clientes/sondeo/funcionalidad-caisy/gestorpedidoalimento", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, sondeo.StatusCode);
    }

    [Fact]
    public async Task CuentaCaisySinLaFuncionNoAccedeAlSondeo()
    {
        var tokenAdmin = await LoginComo(SemillaIdentidad.EmailAdmin, ContrasenaSemilla);
        var emailCaisy = $"caisy-vacia-{Guid.NewGuid():N}@icarus.test";
        var cliente = _factory.CreateClient();

        var alta = await cliente.SendAsync(AutenticadoConContenido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = Array.Empty<string>(),
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var tokenCaisy = await LoginComo(emailCaisy, "Clave-Caisy-123");
        var sondeo = await cliente.SendAsync(Autenticado(
            HttpMethod.Get, "/api/clientes/sondeo/funcionalidad-caisy/gestorpedidoalimento", tokenCaisy));
        Assert.Equal(HttpStatusCode.Forbidden, sondeo.StatusCode);
    }

    [Fact]
    public async Task CuentaDesactivadaNoIniciaSesionNiRenueva()
    {
        var tokenAdmin = await LoginComo(SemillaIdentidad.EmailAdmin, ContrasenaSemilla);
        var emailCaisy = $"caisy-off-{Guid.NewGuid():N}@icarus.test";
        var cliente = _factory.CreateClient();

        var alta = await cliente.SendAsync(AutenticadoConContenido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var cuerpoAlta = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var cuentaId = cuerpoAlta.GetProperty("id").GetString();

        // Sesión válida antes de desactivar: guarda la cookie de refresh.
        var loginInicial = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = emailCaisy, contrasena = "Clave-Caisy-123" });
        Assert.Equal(HttpStatusCode.OK, loginInicial.StatusCode);
        var cookie = loginInicial.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith("icarus_refresh=", StringComparison.Ordinal))
            .Split(';')[0];

        var baja = await cliente.SendAsync(Autenticado(HttpMethod.Delete, $"/api/usuarios-caisy/{cuentaId}", tokenAdmin));
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var loginTardio = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = emailCaisy, contrasena = "Clave-Caisy-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginTardio.StatusCode);

        var renovar = new HttpRequestMessage(HttpMethod.Post, "/api/identidad/sesion/renovar");
        renovar.Headers.Add("Cookie", cookie);
        var renovacion = await cliente.SendAsync(renovar);
        Assert.Equal(HttpStatusCode.Unauthorized, renovacion.StatusCode);
    }
}
