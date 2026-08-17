using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

public class ClientesEndpointsTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public ClientesEndpointsTests(IdentityFactory factory) => _factory = factory;

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

    private static object CuerpoCliente(string identificadorFiscal, string email) => new
    {
        razonSocial = "Granja de Prueba S.A.C.",
        identificadorFiscal,
        email,
        contrasena = IdentityFactory.ContrasenaDePrueba,
    };

    private async Task<Guid> CrearClienteComoAdmin(string identificadorFiscal)
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(
            CuerpoCliente(identificadorFiscal, $"cliente-{Guid.NewGuid():N}@icarus.test"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task CrearClienteComoAdminDevuelve201YLaCuentaEmbebidaPermiteLogin()
    {
        var email = $"cliente-{Guid.NewGuid():N}@icarus.test";
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(
            CuerpoCliente($"2{Random.Shared.Next(100000000, 999999999)}", email));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        // El alta embebida creó la cuenta rol Cliente: el login funciona.
        var login = await LoginComo(email);
        Assert.False(string.IsNullOrEmpty(login));
    }

    [Fact]
    public async Task CrearClienteSinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/clientes",
            CuerpoCliente("20999999999", "nuevo@icarus.test"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearClienteConRolClienteDevuelve403()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(CuerpoCliente("20999999998", "nuevo@icarus.test"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task NitConFormatoInvalidoDevuelve400SinCrearCliente()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var antes = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        var cantidadAntes = (await antes.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(CuerpoCliente("NIT-inválido", $"nuevo-{Guid.NewGuid():N}@icarus.test"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var despues = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        Assert.Equal(cantidadAntes, (await despues.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task IdentificadorFiscalDuplicadoDevuelve409SinDetalle()
    {
        // El identificador del cliente semilla ya existe.
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(
            CuerpoCliente(Icarus.Clientes.Infrastructure.SemillaClientes.IdentificadorFiscalDemo,
                $"nuevo-{Guid.NewGuid():N}@icarus.test"));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Icarus.Clientes.Infrastructure.SemillaClientes.IdentificadorFiscalDemo, texto);
    }

    [Fact]
    public async Task CrearClienteConEmailYaEnUsoDevuelve409SinClienteActivo()
    {
        // El email del cliente semilla ya tiene cuenta (anti-PII: 409 genérico).
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}";
        var pedido = PedidoAutenticado(HttpMethod.Post, "/clientes", token);
        pedido.Content = JsonContent.Create(
            CuerpoCliente(identificadorFiscal, SemillaIdentidad.EmailCliente));

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        // La compensación deja el cliente suspendido: el RIF intentado no
        // aparece entre los clientes activos.
        var lista = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        var activos = (await lista.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Where(c => c.GetProperty("estaActivo").GetBoolean())
            .Select(c => c.GetProperty("identificadorFiscal").GetString())
            .ToList();
        Assert.DoesNotContain(identificadorFiscal, activos);
    }

    [Fact]
    public async Task SuspenderYReactivarCambianElEstadoEnLaLista()
    {
        var id = await CrearClienteComoAdmin($"2{Random.Shared.Next(100000000, 999999999)}");
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var suspender = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{id}/suspender", token));
        Assert.Equal(HttpStatusCode.NoContent, suspender.StatusCode);

        var listaSuspendida = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        var resumenSuspendido = (await listaSuspendida.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id);
        Assert.False(resumenSuspendido.GetProperty("estaActivo").GetBoolean());

        var reactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{id}/reactivar", token));
        Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);

        var listaReactivada = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        var resumenReactivado = (await listaReactivada.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id);
        Assert.True(resumenReactivado.GetProperty("estaActivo").GetBoolean());
    }

    [Fact]
    public async Task SuspenderClienteInexistenteDevuelve404()
    {
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Post, $"/clientes/{Guid.NewGuid()}/suspender", token));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task DefinirModulosComoAdminQuedaReflejadoEnLaLista()
    {
        var id = await CrearClienteComoAdmin($"2{Random.Shared.Next(100000000, 999999999)}");
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Put, $"/clientes/{id}/modulos", token);
        pedido.Content = JsonContent.Create(new { modulos = new[] { "GestionAvicola", "ControlAcceso" } });

        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var lista = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));
        var resumen = (await lista.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id);
        var modulos = resumen.GetProperty("modulos").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Contains("GestionAvicola", modulos);
        Assert.Contains("ControlAcceso", modulos);
    }

    [Fact]
    public async Task DefinirModulosConNombreInvalidoDevuelve400()
    {
        var id = await CrearClienteComoAdmin($"2{Random.Shared.Next(100000000, 999999999)}");
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(HttpMethod.Put, $"/clientes/{id}/modulos", token);
        pedido.Content = JsonContent.Create(new { modulos = new[] { "ModuloQueNoExiste" } });

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ListarClientesConRolClienteDevuelve403()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/clientes", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
