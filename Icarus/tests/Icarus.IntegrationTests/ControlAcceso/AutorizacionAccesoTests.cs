using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Host.Autorizacion;
using Icarus.Host.Endpoints;
using Icarus.Identity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class AutorizacionAccesoTests
{
    private readonly IdentityFactory _factory;

    public AutorizacionAccesoTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
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

    private async Task<(Guid ClienteId, string Token)> CrearClienteConModulo(string[] modulos)
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Control Acceso S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaCliente = await clienteHttp.SendAsync(altaCliente);
        Assert.Equal(HttpStatusCode.Created, respuestaCliente.StatusCode);
        var clienteId = (await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = PedidoAutenticado(HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", admin);
        asignar.Content = JsonContent.Create(new { modulos });
        Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);

        return (clienteId, await LoginComo(email));
    }

    private async Task<(Guid TrabajadorId, string Email, string Token)> CrearTrabajadorActivo(
        Guid clienteId, string tokenCliente)
    {
        var cliente = _factory.CreateClient();
        var email = $"ta-{Guid.NewGuid():N}@icarus.test";
        var altaTrabajador = PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", tokenCliente);
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

        var login = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (trabajadorId, email, cuerpo.GetProperty("accessToken").GetString()!);
    }

    private async Task CesarTrabajador(Guid trabajadorId, string tokenCliente, DateOnly fechaCese)
    {
        var cliente = _factory.CreateClient();
        var cese = PedidoAutenticado(HttpMethod.Post, $"/api/clientes/trabajadores/{trabajadorId}/cese", tokenCliente);
        cese.Content = JsonContent.Create(new { fechaCese = fechaCese.ToString("yyyy-MM-dd") });
        var respuesta = await cliente.SendAsync(cese);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteConControlAccesoPuedeAdministrar()
    {
        var (_, token) = await CrearClienteConModulo(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/administrar", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSinControlAccesoNoPuedeAdministrar()
    {
        var (_, token) = await CrearClienteConModulo(["GestionAvicola"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/administrar", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorActivoPuedeMarcar()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModulo(["ControlAcceso"]);
        var (_, _, tokenTrabajador) = await CrearTrabajadorActivo(clienteId, tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/marcar", tokenTrabajador));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConFechaCeseNoPuedeMarcar()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModulo(["ControlAcceso"]);
        var (trabajadorId, _, tokenTrabajador) = await CrearTrabajadorActivo(clienteId, tokenCliente);
        await CesarTrabajador(trabajadorId, tokenCliente, new DateOnly(2026, 9, 20));
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/marcar", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorDeClienteSinControlAccesoNoPuedeMarcar()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModulo(["GestionAvicola"]);
        var (_, _, tokenTrabajador) = await CrearTrabajadorActivo(clienteId, tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/marcar", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task AdministradorNoPuedeAdministrarNiMarcar()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var admin = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/administrar", token));
        var marcar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/marcar", token));

        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, marcar.StatusCode);
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/control-acceso/sondeo/administrar");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSuspendidoPierdeAccesoInmediatamente()
    {
        var (clienteId, token) = await CrearClienteConModulo(["ControlAcceso"]);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var suspender = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/api/clientes/{clienteId}/suspender", admin));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/administrar", token));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorDesactivadoNoPuedeMarcar()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModulo(["ControlAcceso"]);
        var (trabajadorId, _, tokenTrabajador) = await CrearTrabajadorActivo(clienteId, tokenCliente);

        var cliente = _factory.CreateClient();
        var desactivar = PedidoAutenticado(
            HttpMethod.Delete, $"/api/clientes/trabajadores/{trabajadorId}", tokenCliente);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(desactivar)).StatusCode);

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/sondeo/marcar", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConControlAccesoNoAccedeAvicolaPorSoloModulo()
    {
        // Al contratar Gestión Avícola después, el trabajador no recibe
        // funcionalidades automáticamente: sigue sin poder administrar granjas.
        var (clienteId, tokenCliente) = await CrearClienteConModulo(["ControlAcceso"]);
        var (_, _, tokenTrabajador) = await CrearTrabajadorActivo(clienteId, tokenCliente);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var asignar = PedidoAutenticado(HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", admin);
        asignar.Content = JsonContent.Create(new { modulos = new[] { "ControlAcceso", "GestionAvicola" } });
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(asignar)).StatusCode);

        var avicola = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/api/clientes/sondeo/funcionalidad/granjas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, avicola.StatusCode);
    }
}
