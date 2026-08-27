using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;

namespace Icarus.IntegrationTests;

[Collection(IntegracionCollection.Nombre)]
public class GestionAvicolaEndpointsTests
{
    private readonly IdentityFactory _factory;
    public GestionAvicolaEndpointsTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion", new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Autenticado(HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        var pedido = new HttpRequestMessage(metodo, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (cuerpo is not null) pedido.Content = JsonContent.Create(cuerpo);
        return pedido;
    }

    private async Task<string> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        using var cliente = _factory.CreateClient();
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Autenticado(HttpMethod.Put, $"/api/clientes/{id}/modulos", admin, new { modulos = new[] { "GestionAvicola" } }));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return await LoginComo(email);
    }

    [Fact]
    public async Task ClienteSemillaListaSuGranjaYGalpones()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        using var cliente = _factory.CreateClient();
        var granjas = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/granjas", token));
        Assert.Equal(HttpStatusCode.OK, granjas.StatusCode);
        var lista = await granjas.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, lista.GetArrayLength());
        var id = lista[0].GetProperty("id").GetGuid();
        var galpones = await cliente.SendAsync(Autenticado(HttpMethod.Get, $"/api/granjas/{id}/galpones", token));
        Assert.Equal(HttpStatusCode.OK, galpones.StatusCode);
        Assert.Equal(2, (await galpones.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task ClienteCreaUnaGranjaYLaSegundaDevuelve409()
    {
        var token = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var primera = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/granjas", token, new { nombre = $"Granja {Guid.NewGuid():N}" }));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        var segunda = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/granjas", token, new { nombre = $"Otra {Guid.NewGuid():N}" }));
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task IdDeOtroTenantDevuelve404()
    {
        var clienteToken = await CrearClienteAvicola();
        var otroClienteToken = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/granjas", clienteToken, new { nombre = "Granja Tenant A" }));
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var otro = await cliente.SendAsync(Autenticado(HttpMethod.Get, $"/api/granjas/{id}", otroClienteToken));
        Assert.Equal(HttpStatusCode.NotFound, otro.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConProduccionPuedeConsultarGalpones()
    {
        var clienteToken = await LoginComo(SemillaIdentidad.EmailCliente);
        var trabajadorToken = await LoginComo(SemillaIdentidad.EmailTrabajador);
        using var cliente = _factory.CreateClient();
        var granjas = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/granjas", clienteToken));
        var id = (await granjas.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var respuesta = await cliente.SendAsync(Autenticado(HttpMethod.Get, $"/api/granjas/{id}/galpones", trabajadorToken));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task GalponConFechaFuturaDevuelve400()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        using var cliente = _factory.CreateClient();
        var granjas = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/granjas", token));
        var id = (await granjas.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        var respuesta = await cliente.SendAsync(Autenticado(HttpMethod.Post, $"/api/granjas/{id}/galpones", token, new { numero = $"F{Guid.NewGuid():N}"[..10], capacidadMaxima = 100, gallinasActuales = 0, fechaNacimientoLote = fecha }));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/granjas");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
