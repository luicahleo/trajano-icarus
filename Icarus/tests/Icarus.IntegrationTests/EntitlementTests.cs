using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Host.Endpoints;
using Icarus.Identity.Infrastructure;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// El mecanismo de entitlement se construye y se prueba en este incremento
// aunque aún no haya endpoints de módulos de negocio (spec): el sondeo lo
// ejercita de punta a punta.
[Collection(IntegracionCollection.Nombre)]
public class EntitlementTests
{
    private readonly IdentityFactory _factory;

    public EntitlementTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    // Alta embebida de un cliente y su cuenta de acceso de rol Cliente, con
    // asignación opcional de módulos. Devuelve el id y el token de la cuenta.
    private async Task<(Guid ClienteId, string Token)> CrearClienteConCuenta(string[]? modulos)
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();

        var email = $"cuenta-{Guid.NewGuid():N}@icarus.test";
        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Granja de Prueba S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaCliente = await clienteHttp.SendAsync(altaCliente);
        Assert.Equal(HttpStatusCode.Created, respuestaCliente.StatusCode);
        var clienteId = (await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        if (modulos is not null)
        {
            var asignar = PedidoAutenticado(HttpMethod.Put, $"/clientes/{clienteId}/modulos", admin);
            asignar.Content = JsonContent.Create(new { modulos });
            Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);
        }

        return (clienteId, await LoginComo(email));
    }

    private async Task<(Guid ClienteId, string Email, string Token, string Cookie)> CrearClienteConSesion(
        string[]? modulos)
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"cuenta-{Guid.NewGuid():N}@icarus.test";
        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Granja de Prueba S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaCliente = await clienteHttp.SendAsync(altaCliente);
        Assert.Equal(HttpStatusCode.Created, respuestaCliente.StatusCode);
        var clienteId = (await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        if (modulos is not null)
        {
            var asignar = PedidoAutenticado(HttpMethod.Put, $"/clientes/{clienteId}/modulos", admin);
            asignar.Content = JsonContent.Create(new { modulos });
            Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);
        }

        var login = await clienteHttp.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(IdentidadEndpoints.CookieRefresh + "=", StringComparison.Ordinal))
            .Split(';')[0];
        return (clienteId, email, cuerpo.GetProperty("accessToken").GetString()!, cookie);
    }

    // Alta embebida de un trabajador con asignación opcional de funcionalidades.
    // Devuelve el token de su cuenta de rol Trabajador.
    private async Task<(string Email, string Token, string Cookie)> CrearTrabajadorConCuenta(
        Guid clienteId, string[]? funcionalidades, string tokenCliente)
    {
        var cliente = _factory.CreateClient();
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var altaTrabajador = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", tokenCliente);
        altaTrabajador.Content = JsonContent.Create(new
        {
            nombre = "Nombre Ficticio",
            documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaAlta = await cliente.SendAsync(altaTrabajador);
        Assert.Equal(HttpStatusCode.Created, respuestaAlta.StatusCode);
        var trabajadorId = (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        if (funcionalidades is not null)
        {
            var asignar = PedidoAutenticado(
                HttpMethod.Put, $"/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades",
                tokenCliente);
            asignar.Content = JsonContent.Create(new { funcionalidades });
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(asignar)).StatusCode);
        }

        var login = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(IdentidadEndpoints.CookieRefresh + "=", StringComparison.Ordinal))
            .Split(';')[0];
        return (email, cuerpo.GetProperty("accessToken").GetString()!, cookie);
    }

    [Fact]
    public async Task ClienteConModuloHabilitadoRecibe200()
    {
        // El rol Cliente tiene todas las funcionalidades de sus módulos: el
        // cliente semilla tiene GestionAvicola.
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var granjas = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", token));
        var vacunacion = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/vacunacion", token));

        Assert.Equal(HttpStatusCode.OK, granjas.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vacunacion.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConFuncionalidadAsignadaRecibe200()
    {
        // El trabajador semilla tiene Granjas asignado (sembrado en este plan).
        var token = await LoginComo(SemillaIdentidad.EmailTrabajador);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorSinFuncionalidadAsignadaDevuelve403()
    {
        // El trabajador semilla no tiene Vacunacion.
        var token = await LoginComo(SemillaIdentidad.EmailTrabajador);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/vacunacion", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorNuevoSinFuncionalidadesDevuelve403()
    {
        var (clienteId, tokenCliente) = await CrearClienteConCuenta(["GestionAvicola"]);
        var (_, tokenTrabajador, _) = await CrearTrabajadorConCuenta(
            clienteId, funcionalidades: null, tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConFuncionalidadAsignadaViaEndpointRecibe200()
    {
        var (clienteId, tokenCliente) = await CrearClienteConCuenta(["GestionAvicola"]);
        var (_, tokenTrabajador, _) = await CrearTrabajadorConCuenta(
            clienteId, ["granjas"], tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSinModulosRecibe403()
    {
        var (_, token) = await CrearClienteConCuenta(modulos: null);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSuspendidoPierdeElEntitlement()
    {
        var (clienteId, token) = await CrearClienteConCuenta(["GestionAvicola"]);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        // El entitlement se lee de la BD en cada request: suspender después
        // del login también corta el acceso.
        var suspender = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/suspender", admin));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", token));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSuspendidoBloqueaLoginRenovacionYTokensYaEmitidos()
    {
        var (clienteId, email, token, cookie) = await CrearClienteConSesion(["GestionAvicola"]);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var suspender = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/suspender", admin));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        var login = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var renovar = new HttpRequestMessage(HttpMethod.Post, "/identidad/sesion/renovar");
        renovar.Headers.Add("Cookie", cookie);
        var respuestaRenovacion = await cliente.SendAsync(renovar);
        Assert.Equal(HttpStatusCode.Unauthorized, respuestaRenovacion.StatusCode);

        var me = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/identidad/me", token));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        var trabajadores = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/clientes/{clienteId}/trabajadores", token));
        Assert.Equal(HttpStatusCode.Unauthorized, trabajadores.StatusCode);
    }

    [Fact]
    public async Task ClienteSuspendidoBloqueaLaSesionDeSuTrabajador()
    {
        var (clienteId, tokenCliente) = await CrearClienteConCuenta(["GestionAvicola"]);
        var (emailTrabajador, tokenTrabajador, cookieTrabajador) = await CrearTrabajadorConCuenta(
            clienteId, ["granjas"], tokenCliente);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var suspender = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/suspender", admin));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        var login = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email = emailTrabajador, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var renovar = new HttpRequestMessage(HttpMethod.Post, "/identidad/sesion/renovar");
        renovar.Headers.Add("Cookie", cookieTrabajador);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.SendAsync(renovar)).StatusCode);

        var me = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/identidad/me", tokenTrabajador));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task TrabajadorSinClienteNoPuedeIniciarSesion()
    {
        var (clienteId, tokenCliente) = await CrearClienteConCuenta(["GestionAvicola"]);
        var (emailTrabajador, _, _) = await CrearTrabajadorConCuenta(
            clienteId, ["granjas"], tokenCliente);

        using (var alcance = _factory.Services.CreateScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var trabajador = await usuarios.FindByEmailAsync(emailTrabajador);
            Assert.NotNull(trabajador);
            trabajador.ClienteId = null;
            Assert.True((await usuarios.UpdateAsync(trabajador)).Succeeded);
        }

        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email = emailTrabajador, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task RolAdministradorNoPasaElEntitlement()
    {
        // Los roles de plataforma no llevan clienteId: el entitlement no aplica.
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/clientes/sondeo/funcionalidad/granjas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
