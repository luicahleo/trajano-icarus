using System.Net;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

public class FlujoSesionTests
{
    [Fact]
    public async Task LaRaizSinSesionRedirigeAAcceder()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CrearClienteSinRedireccion();

        var respuesta = await cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Acceder", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task AccederMuestraFormularioConEtiquetasYAntiforgery()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CrearClienteSinRedireccion();

        var html = await cliente.GetStringAsync("/Sesion/Acceder");

        Assert.Contains("lang=\"es\"", html);
        Assert.Contains("Saltar al contenido", html);
        Assert.Contains("for=\"Correo\"", html);
        Assert.Contains("for=\"Contrasena\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.DoesNotContain("autofocus", html);
    }

    [Fact]
    public async Task PostSinTokenAntiforgeryEsRechazado()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CrearClienteSinRedireccion();

        var respuesta = await cliente.PostAsync("/Sesion/Acceder", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = AplicacionDePruebas.CorreoValido,
                ["Contrasena"] = AplicacionDePruebas.ClaveValida,
            }));

        Assert.True(
            respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.MethodNotAllowed,
            $"Se esperaba el rechazo antiforgery, llegó {(int)respuesta.StatusCode}");
        Assert.Equal(0, aplicacion.Api.IniciosDeSesion);
    }

    [Fact]
    public async Task AccesoValidoEstableceCookieHttpOnlySinTokensEnClaro()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CrearClienteSinRedireccion();
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/Sesion/Acceder");

        var respuesta = await cliente.PostAsync("/Sesion/Acceder", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = AplicacionDePruebas.CorreoValido,
                ["Contrasena"] = AplicacionDePruebas.ClaveValida,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Precios", respuesta.Headers.Location?.OriginalString);
        var cookieDeSesion = respuesta.Headers
            .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value.FirstOrDefault(v => v.StartsWith("trajano_gestorcaisy=", StringComparison.Ordinal));
        Assert.NotNull(cookieDeSesion);
        Assert.Contains("HttpOnly", cookieDeSesion, StringComparison.OrdinalIgnoreCase);
        // Anti-PII y defensa en profundidad: el JWT y el refresh token nunca
        // viajan en claro dentro de la cookie ni del HTML.
        Assert.DoesNotContain("eyJ", cookieDeSesion);
        Assert.DoesNotContain(AplicacionDePruebas.ClaveValida, cookieDeSesion);

        var precios = await cliente.GetAsync("/Precios");
        Assert.Equal(HttpStatusCode.OK, precios.StatusCode);
        Assert.Contains("Precios de alimento", await precios.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CredencialesInvalidasMuestraMensajeGenericoSinCookie()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CrearClienteSinRedireccion();
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/Sesion/Acceder");
        aplicacion.Api.AlIniciarSesion = (_, _) => throw new ErrorApiException(401, "No autorizado");

        var respuesta = await cliente.PostAsync("/Sesion/Acceder", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = "intruso@caisy.test",
                ["Contrasena"] = "clave-errada",
                ["__RequestVerificationToken"] = token,
            }));

        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos", html);
        Assert.DoesNotContain("intruso@caisy.test", html);
        Assert.All(respuesta.Headers,
            h => Assert.NotEqual("Set-Cookie", h.Key, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SinLaFuncionalidadLaRutaDePreciosQuedaProhibida()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 0);

        var respuesta = await cliente.GetAsync("/Precios");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Denegado", respuesta.Headers.Location!.AbsolutePath);
        var denegado = await cliente.GetStringAsync("/Sesion/Denegado");
        Assert.Contains("No tiene acceso", denegado);
    }

    [Fact]
    public async Task CerrarSesionEliminaLaCookie()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/Precios");

        var salida = await cliente.PostAsync("/Sesion/Salir", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, salida.StatusCode);
        Assert.Equal("/Sesion/Acceder", salida.Headers.Location?.OriginalString);
        var precios = await cliente.GetAsync("/Precios");
        Assert.Equal(HttpStatusCode.Redirect, precios.StatusCode);
        Assert.Equal("/Sesion/Acceder", precios.Headers.Location!.AbsolutePath);
    }
}
