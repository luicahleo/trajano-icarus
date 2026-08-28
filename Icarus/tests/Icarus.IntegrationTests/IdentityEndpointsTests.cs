using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Host.Endpoints;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

[Collection(IntegracionCollection.Nombre)]
public class IdentityEndpointsTests
{
    private readonly IdentityFactory _factory;

    public IdentityEndpointsTests(IdentityFactory factory) => _factory = factory;

    private static string ContrasenaSemilla => IdentityFactory.ContrasenaDePrueba;

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = ContrasenaSemilla });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    [Fact]
    public async Task LoginDevuelveAccessTokenYCookieRefreshHttpOnly()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = SemillaIdentidad.EmailAdmin, contrasena = ContrasenaSemilla });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(cuerpo.GetProperty("accessToken").GetString()));

        var setCookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(IdentidadEndpoints.CookieRefresh + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginConContrasenaIncorrectaDevuelve401SinDetalle()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = SemillaIdentidad.EmailAdmin, contrasena = "incorrecta" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SemillaIdentidad.EmailAdmin, texto);
        Assert.DoesNotContain("contraseña", texto);
        Assert.DoesNotContain("contrasena", texto);
    }

    [Fact]
    public async Task LoginConEmailInexistenteDevuelveLaMismaRespuesta()
    {
        // Anti-enumeración: email inexistente y contraseña incorrecta deben ser
        // indistinguibles en status y cuerpo.
        var cliente = _factory.CreateClient();

        var emailInexistente = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = "nadie@icarus.test", contrasena = "x" });
        var contrasenaIncorrecta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = SemillaIdentidad.EmailAdmin, contrasena = "x" });

        Assert.Equal(emailInexistente.StatusCode, contrasenaIncorrecta.StatusCode);
        var textoInexistente = await emailInexistente.Content.ReadAsStringAsync();
        var textoIncorrecta = await contrasenaIncorrecta.Content.ReadAsStringAsync();
        Assert.Equal(
            SinIdentificadoresTecnicos(textoInexistente),
            SinIdentificadoresTecnicos(textoIncorrecta));
    }

    // El cuerpo incluye los IDs técnicos propios de cada petición. Se normalizan
    // porque la regla anti-enumeración es sobre el status y el detalle funcional.
    private static string SinIdentificadoresTecnicos(string texto)
    {
        using var doc = JsonDocument.Parse(texto);
        var correlationId = doc.RootElement.GetProperty("correlationId").GetString()!;
        var traceId = doc.RootElement.GetProperty("traceId").GetString()!;
        return texto
            .Replace(correlationId, "*", StringComparison.Ordinal)
            .Replace(traceId, "*", StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeDevuelveLosClaimsDelToken()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cliente", cuerpo.GetProperty("rol").GetString());
        Assert.Equal(
            SemillaIdentidad.ClienteDemoId,
            Guid.Parse(cuerpo.GetProperty("clienteId").GetString()!));
    }

    [Fact]
    public async Task MeDevuelveElCorreoDelUsuarioAutenticado()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(SemillaIdentidad.EmailCliente, cuerpo.GetProperty("correo").GetString());
    }

    [Fact]
    public async Task MeComoClienteDevuelveModulosYTodasLasFuncionalidades()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailCliente);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/api/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var modulos = cuerpo.GetProperty("modulos").EnumerateArray().Select(e => e.GetString()).ToList();
        var funcionalidades = cuerpo.GetProperty("funcionalidades").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("GestionAvicola", modulos);
        Assert.Contains("ProduccionHuevos", funcionalidades);
        Assert.Contains("Mortalidad", funcionalidades);
    }

    [Fact]
    public async Task MeComoTrabajadorDevuelveSoloSusFuncionalidadesAsignadas()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailTrabajador);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/api/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var modulos = cuerpo.GetProperty("modulos").EnumerateArray().Select(e => e.GetString()).ToList();
        var funcionalidades = cuerpo.GetProperty("funcionalidades").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Empty(modulos);
        Assert.Equal(["ProduccionHuevos"], funcionalidades);
    }

    [Fact]
    public async Task MeComoAdminDevuelveListasVacias()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/api/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(cuerpo.GetProperty("modulos").EnumerateArray());
        Assert.Empty(cuerpo.GetProperty("funcionalidades").EnumerateArray());
    }

    [Fact]
    public async Task MeSinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/identidad/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task RenovarConCookieValidaDaNuevaSesionYRevocaElRefreshAnterior()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = SemillaIdentidad.EmailAdmin, contrasena = ContrasenaSemilla });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(IdentidadEndpoints.CookieRefresh + "=", StringComparison.Ordinal))
            .Split(';')[0];

        var renovar1 = new HttpRequestMessage(HttpMethod.Post, "/api/identidad/sesion/renovar");
        renovar1.Headers.Add("Cookie", cookie);
        var respuesta1 = await cliente.SendAsync(renovar1);
        Assert.Equal(HttpStatusCode.OK, respuesta1.StatusCode);

        // El refresh original ya fue rotado: reusarlo es un 401.
        var renovar2 = new HttpRequestMessage(HttpMethod.Post, "/api/identidad/sesion/renovar");
        renovar2.Headers.Add("Cookie", cookie);
        var respuesta2 = await cliente.SendAsync(renovar2);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta2.StatusCode);
    }

    [Fact]
    public async Task RenovacionesConcurrentesConElMismoRefreshSoloUnaRota()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email = SemillaIdentidad.EmailAdmin, contrasena = ContrasenaSemilla });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(IdentidadEndpoints.CookieRefresh + "=", StringComparison.Ordinal))
            .Split(';')[0];

        // Dos renovaciones en vuelo a la vez con la misma cookie (p. ej. dos
        // pestañas o StrictMode de React): solo una puede rotar el refresh.
        HttpRequestMessage PedidoRenovacion()
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/identidad/sesion/renovar");
            pedido.Headers.Add("Cookie", cookie);
            return pedido;
        }

        var respuestas = await Task.WhenAll(
            cliente.SendAsync(PedidoRenovacion()),
            cliente.SendAsync(PedidoRenovacion()));

        var codigos = respuestas.Select(r => r.StatusCode).Order().ToArray();
        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized },
            codigos);
    }

    [Fact]
    public async Task RenovarSinCookieDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsync("/api/identidad/sesion/renovar", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
