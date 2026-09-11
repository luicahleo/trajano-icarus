using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9F: la bandeja de notificaciones del despacho de huevo del tenant esconde
// los tipos financieros al rol Trabajador y se los muestra completos al
// Cliente. Las notificaciones se siembran directo en el DbContext: el
// objetivo de esta clase es la visibilidad por rol, no el flujo que las
// origina (cubierto por CorreccionPrecioHuevoTests y
// DespachosHuevoCaisyEndpointsTests).
[Collection(IntegracionCollection.Nombre)]
public class NotificacionesDespachoHuevoEndpointsTests
{
    private readonly IdentityFactory _factory;

    public NotificacionesDespachoHuevoEndpointsTests(IdentityFactory factory) => _factory = factory;

    private static async Task<string> LoginComo(HttpClient cliente, string email, string? contrasena = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = contrasena ?? IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Pedido(
        HttpMethod metodo, string url, string token, HttpContent? contenido = null) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = contenido,
        };

    // Tenant nuevo con el módulo GestionAvicola (habilita DespachoHuevo para
    // el Cliente). Duplicado a propósito para que el archivo quede
    // autocontenido, igual que en DespachosHuevoCaisyEndpointsTests.
    private async Task<(HttpClient Cliente, string Token, Guid ClienteId)> CrearClienteConGestionAvicolaAsync()
    {
        var cliente = _factory.CreateClient();
        var tokenAdmin = await LoginComo(cliente, SemillaIdentidad.EmailAdmin);
        var email = $"notificaciones-huevo-cliente-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", tokenAdmin,
            JsonContent.Create(new
            {
                razonSocial = "Granja de Prueba S.A.C.",
                identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
                email,
                contrasena = "Clave-Cliente-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var clienteId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", tokenAdmin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (cliente, await LoginComo(cliente, email, "Clave-Cliente-123"), clienteId);
    }

    private static async Task<string> CrearTrabajadorConFuncionAsync(
        HttpClient cliente, string tokenCliente, Guid clienteId, string funcionalidad)
    {
        var email = $"notificaciones-huevo-trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post,
            $"/api/clientes/{clienteId}/trabajadores", tokenCliente,
            JsonContent.Create(new
            {
                nombre = "Trabajador de Prueba",
                documentoIdentidad = $"9{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Operario",
                fechaIngreso = "2026-01-15",
                email,
                contrasena = "Clave-Trabajador-123",
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = await cliente.SendAsync(Pedido(HttpMethod.Put,
            $"/api/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            JsonContent.Create(new { funcionalidades = new[] { funcionalidad } })));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        return await LoginComo(cliente, email, "Clave-Trabajador-123");
    }

    private async Task SembrarNotificacionesAsync(Guid clienteId, Guid despachoId)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
        db.NotificacionesInternasDespachoHuevo.AddRange(
            NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(despachoId, clienteId),
            NotificacionInternaDespachoHuevo.ParaAjusteCredito(
                despachoId, clienteId, "27.0000 Bs — Precio mal digitado."));
        await db.SaveChangesAsync();
    }

    private static async Task<(List<string> Tipos, int Contador)> LeerBandejaAsync(
        HttpClient cliente, string token)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo/notificaciones", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var tipos = cuerpo.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("tipo").GetString()!)
            .ToList();
        return (tipos, cuerpo.GetProperty("contador").GetInt32());
    }

    [Fact]
    public async Task ElClienteVeLosDosTiposYElTrabajadorSoloElOperativo()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "DespachoHuevo");
        await SembrarNotificacionesAsync(clienteId, Guid.NewGuid());

        var (tiposCliente, contadorCliente) = await LeerBandejaAsync(cliente, tokenCliente);
        var (tiposTrabajador, contadorTrabajador) = await LeerBandejaAsync(cliente, tokenTrabajador);

        Assert.Contains("DespachoRecibido", tiposCliente);
        Assert.Contains("AjusteCredito", tiposCliente);
        Assert.Equal(2, contadorCliente);

        Assert.Contains("DespachoRecibido", tiposTrabajador);
        Assert.DoesNotContain("AjusteCredito", tiposTrabajador);
        Assert.Equal(1, contadorTrabajador);
    }

    [Fact]
    public async Task ElTrabajadorNoPuedeMarcarLeidaLaNotificacionDeAjuste()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "DespachoHuevo");
        await SembrarNotificacionesAsync(clienteId, Guid.NewGuid());

        var (_, _) = await LeerBandejaAsync(cliente, tokenCliente);
        var respuestaCliente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo/notificaciones", tokenCliente));
        var cuerpo = await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>();
        var idAjuste = cuerpo.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("tipo").GetString() == "AjusteCredito")
            .GetProperty("id").GetGuid();

        var intento = await cliente.SendAsync(Pedido(HttpMethod.Post,
            $"/api/despachos-huevo/notificaciones/{idAjuste}/marcar-leida", tokenTrabajador));

        Assert.Equal(HttpStatusCode.NotFound, intento.StatusCode);
    }
}
