using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// El mecanismo de entitlement se construye y se prueba en este incremento
// aunque aún no haya endpoints de módulos de negocio (spec): el sondeo lo
// ejercita de punta a punta.
public class EntitlementTests : IClassFixture<IdentityFactory>
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

    // Crea un cliente y una cuenta rol Cliente vinculada; devuelve el id del
    // cliente y el token de la cuenta nueva.
    private async Task<(Guid ClienteId, string Token)> CrearClienteConCuenta(string[]? modulos)
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();

        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Granja de Prueba S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email = $"cuenta-{Guid.NewGuid():N}@icarus.test",
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

        var email = $"cuenta-{Guid.NewGuid():N}@icarus.test";
        var altaUsuario = PedidoAutenticado(HttpMethod.Post, "/identidad/usuarios", admin);
        altaUsuario.Content = JsonContent.Create(new
        {
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
            rol = "Cliente",
            clienteId,
        });
        Assert.Equal(HttpStatusCode.Created, (await clienteHttp.SendAsync(altaUsuario)).StatusCode);

        return (clienteId, await LoginComo(email));
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
        var (clienteId, _) = await CrearClienteConCuenta(["GestionAvicola"]);
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();

        var altaTrabajador = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", admin);
        altaTrabajador.Content = JsonContent.Create(new
        {
            nombre = "Nombre Ficticio",
            documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email = $"trabajador-{Guid.NewGuid():N}@icarus.test",
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaAlta = await clienteHttp.SendAsync(altaTrabajador);
        Assert.Equal(HttpStatusCode.Created, respuestaAlta.StatusCode);
        var trabajadorId = (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var altaUsuario = PedidoAutenticado(HttpMethod.Post, "/identidad/usuarios", admin);
        altaUsuario.Content = JsonContent.Create(new
        {
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
            rol = "Trabajador",
            clienteId,
            trabajadorId,
        });
        Assert.Equal(HttpStatusCode.Created, (await clienteHttp.SendAsync(altaUsuario)).StatusCode);

        var tokenTrabajador = await LoginComo(email);
        var respuesta = await clienteHttp.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/clientes/sondeo/funcionalidad/granjas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
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

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
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
