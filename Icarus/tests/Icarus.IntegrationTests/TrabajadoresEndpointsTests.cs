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

    // Crea un cliente con su cuenta embebida y devuelve id, email y token del
    // rol Cliente nuevo (para gestionar sus propios trabajadores).
    private async Task<(Guid ClienteId, string Token)> CrearClienteConCuenta()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var email = $"cliente-{Guid.NewGuid():N}@icarus.test";
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(new
        {
            razonSocial = "Granja de Prueba S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var clienteId = (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        return (clienteId, await LoginComo(email));
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
    public async Task CrearTrabajadorComoClienteEnSuEmpresaDevuelve201YLaCuentaEmbebidaPermiteLogin()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();
        var documento = $"8{Random.Shared.Next(10000000, 99999999)}";
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(new
        {
            nombre = "Nombre Ficticio",
            documentoIdentidad = documento,
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        // El alta embebida creó la cuenta rol Trabajador: el login funciona.
        var tokenTrabajador = await LoginComo(email);
        Assert.False(string.IsNullOrEmpty(tokenTrabajador));
    }

    [Fact]
    public async Task CrearTrabajadorComoClienteEnEmpresaAjenaDevuelve404()
    {
        var (clienteAjenoId, _) = await CrearClienteConCuenta();
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
    public async Task AdministradorYaNoGestionaTrabajadoresDevuelve403()
    {
        // Spec: la gestión de trabajadores pasa a ser exclusiva del rol Cliente.
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{SemillaIdentidad.ClienteDemoId}/trabajadores", token);
        pedido.Content = JsonContent.Create(CuerpoTrabajador("89999998"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ListarTrabajadoresComoClienteSoloVeLosDeSuEmpresa()
    {
        var (clienteAjenoId, tokenAjeno) = await CrearClienteConCuenta();
        var clienteHttp = _factory.CreateClient();
        var altaAjena = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{clienteAjenoId}/trabajadores", tokenAjeno);
        altaAjena.Content = JsonContent.Create(CuerpoTrabajador("89999997"));
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
        pedido.Content = JsonContent.Create(CuerpoTrabajador("89999996"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task DocumentoDuplicadoEnElMismoClienteDevuelve409SinDetalle()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
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
        var (otroClienteId, tokenOtro) = await CrearClienteConCuenta();
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/clientes/{otroClienteId}/trabajadores", tokenOtro);
        pedido.Content = JsonContent.Create(
            CuerpoTrabajador(Icarus.Clientes.Infrastructure.SemillaClientes.DocumentoTrabajadorDemo));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task CeseConFechaFuturaDevuelve400()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
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
        var (clienteId, token) = await CrearClienteConCuenta();
        var cliente = _factory.CreateClient();
        var alta = PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", token);
        alta.Content = JsonContent.Create(CuerpoTrabajador("89999995"));
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

    [Fact]
    public async Task AsignarFuncionalidadesDevuelve204YQuedanEnLaLista()
    {
        var (clienteId, token) = await CrearClienteConCuenta();
        var cliente = _factory.CreateClient();
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var asignarModulos = PedidoAutenticado(HttpMethod.Put, $"/clientes/{clienteId}/modulos", admin);
        asignarModulos.Content = JsonContent.Create(new { modulos = new[] { "GestionAvicola" } });
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(asignarModulos)).StatusCode);

        var alta = PedidoAutenticado(HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", token);
        alta.Content = JsonContent.Create(CuerpoTrabajador("89999994"));
        var respuestaAlta = await cliente.SendAsync(alta);
        var trabajadorId = (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = PedidoAutenticado(
            HttpMethod.Put, $"/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", token);
        asignar.Content = JsonContent.Create(new { funcionalidades = new[] { "Granjas", "precios" } });
        var respuestaAsignar = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.NoContent, respuestaAsignar.StatusCode);

        var lista = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/clientes/{clienteId}/trabajadores", token));
        var resumen = (await lista.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == trabajadorId);
        var funcionalidades = resumen.GetProperty("funcionalidades").EnumerateArray()
            .Select(f => f.GetString()).ToList();
        Assert.Contains("Granjas", funcionalidades);
        Assert.Contains("Precios", funcionalidades);
    }
}
