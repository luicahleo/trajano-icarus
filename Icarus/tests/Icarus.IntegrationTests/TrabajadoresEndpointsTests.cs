using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

public class TrabajadoresEndpointsTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public TrabajadoresEndpointsTests(IdentityFactory factory) => _factory = factory;

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

    private async Task<Guid> CrearClienteComoAdmin()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(new
        {
            razonSocial = "Granja de Prueba S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email = $"cliente-{Guid.NewGuid():N}@icarus.test",
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("id").GetGuid();
    }

    private static object CuerpoTrabajador(string documento) => new
    {
        nombre = "Nombre Ficticio",
        documentoIdentidad = documento,
        cargo = "Operario",
        fechaIngreso = "2026-01-15",
        email = $"trabajador-{Guid.NewGuid():N}@icarus.test",
        contrasena = IdentityFactory.ContrasenaDePrueba,
    };

    [Fact]
    public async Task CrearTrabajadorComoClienteEnSuEmpresaDevuelve201()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();
        var documento = $"8{Random.Shared.Next(10000000, 99999999)}";
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(CuerpoTrabajador(documento));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearTrabajadorComoClienteEnEmpresaAjenaDevuelve404()
    {
        var clienteAjenoId = await CrearClienteComoAdmin();
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{clienteAjenoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(CuerpoTrabajador("89999999"));

        var respuesta = await cliente.SendAsync(pedido);

        // Anti-enumeración: el tenant ajeno es indistinguible de uno inexistente.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ListarTrabajadoresComoClienteSoloVeLosDeSuEmpresa()
    {
        var clienteAjenoId = await CrearClienteComoAdmin();
        var tokenAdmin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var altaAjena = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{clienteAjenoId}/trabajadores", tokenAdmin);
        altaAjena.Content = JsonContent.Create(CuerpoTrabajador("89999998"));
        Assert.Equal(HttpStatusCode.Created, (await clienteHttp.SendAsync(altaAjena)).StatusCode);

        var tokenCliente = await LoginComo(SemillaIdentidad.EmailCliente);

        // La empresa ajena existe pero el filtro de tenant la vacía.
        var listaAjena = await clienteHttp.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/clientes/{clienteAjenoId}/trabajadores", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, listaAjena.StatusCode);
        Assert.Empty((await listaAjena.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        // La propia lista al trabajador semilla.
        var listaPropia = await clienteHttp.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", tokenCliente));
        var propios = (await listaPropia.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Contains(propios,
            t => t.GetProperty("id").GetGuid() == SemillaIdentidad.TrabajadorDemoId);
    }

    [Fact]
    public async Task CrearTrabajadorConRolTrabajadorDevuelve403()
    {
        var token = await LoginComo(SemillaIdentidad.EmailTrabajador);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(CuerpoTrabajador("89999997"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task DocumentoDuplicadoEnElMismoClienteDevuelve409SinDetalle()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(
            CuerpoTrabajador(Icarus.Clientes.Infrastructure.SemillaClientes.DocumentoTrabajadorDemo));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Icarus.Clientes.Infrastructure.SemillaClientes.DocumentoTrabajadorDemo, texto);
    }

    [Fact]
    public async Task MismoDocumentoEnOtroClienteSePermite()
    {
        var otroClienteId = await CrearClienteComoAdmin();
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{otroClienteId}/trabajadores", token);
        pedido.Content = JsonContent.Create(
            CuerpoTrabajador(Icarus.Clientes.Infrastructure.SemillaClientes.DocumentoTrabajadorDemo));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task CeseConFechaFuturaDevuelve400()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var futura = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd");
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/trabajadores/{SemillaIdentidad.TrabajadorDemoId}/cese", token);
        pedido.Content = JsonContent.Create(new { fechaCese = futura });

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task DesactivarTrabajadorLoQuitaDeLaListaSinBorrarlo()
    {
        var clienteId = await CrearClienteComoAdmin();
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var alta = PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", token);
        alta.Content = JsonContent.Create(CuerpoTrabajador("89999996"));
        var respuestaAlta = await cliente.SendAsync(alta);
        var trabajadorId = (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var baja = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/clientes/trabajadores/{trabajadorId}", token));
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var lista = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/clientes/{clienteId}/trabajadores", token));
        var restantes = (await lista.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.DoesNotContain(restantes, t => t.GetProperty("id").GetGuid() == trabajadorId);
    }
}
