using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class SesionKioscoTests
{
    private const string OrigenKiosco = "https://kiosco.icarus.test";
    private const string OrigenHermano = "https://otro.icarus.test";

    private readonly IdentityFactory _factory;

    public SesionKioscoTests(IdentityFactory factory) => _factory = factory;

    private static ControlAccesoDbContext NuevoContexto(Guid? clienteId, string cadena)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioDePrueba(clienteId));
    }

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private static HttpRequestMessage PedidoKiosco(HttpMethod metodo, string url, string cookie)
    {
        var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.Add("Cookie", cookie);
        return pedido;
    }

    private async Task<string> LoginComoAsync(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private async Task<(Guid ClienteId, string Email, string Token)> CrearClienteConModuloAsync(
        string[] modulos)
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Kiosco S.A.C.",
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

        return (clienteId, email, await LoginComoAsync(email));
    }

    private async Task<string> CrearTrabajadorActivoAsync(Guid clienteId, string tokenCliente)
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
        return email;
    }

    private static HttpRequestMessage Activacion(string email,
        string? origen = null, bool conAntiforgery = true, Guid? clienteFalsificado = null)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion");
        pedido.Content = JsonContent.Create(new
        {
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
            clienteId = clienteFalsificado,
        });
        if (conAntiforgery)
            pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        if (origen is not null)
            pedido.Headers.Add("Origin", origen);
        return pedido;
    }

    private async Task<string> ActivarKioscoAsync(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(Activacion(email, OrigenKiosco));
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        return ExtraerCookie(respuesta);
    }

    private static string ExtraerCookie(HttpResponseMessage respuesta)
    {
        var setCookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(OpcionesKiosco.Cookie + "=", StringComparison.Ordinal));
        return setCookie.Split(';')[0];
    }

    [Fact]
    public async Task ActivacionCreaCookieRestringidaSinJwtNiRefresh()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Activacion(email, OrigenKiosco));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var setCookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(OpcionesKiosco.Cookie + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            respuesta.Headers.GetValues("Set-Cookie"),
            h => h.StartsWith("icarus_refresh=", StringComparison.Ordinal));
        Assert.Empty(await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CookieDeKioscoNoAccedeAAdministracion()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookie = await ActivarKioscoAsync(email);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/sondeo/administrar", cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task SesionKioscoVigente_PermiteConsultarSuPropioEstado()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookie = await ActivarKioscoAsync(email);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookie));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("expiraEnUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SegundaActivacionRevocaLaPrimera()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookieA = await ActivarKioscoAsync(email);
        var cookieB = await ActivarKioscoAsync(email);
        var cliente = _factory.CreateClient();

        var primera = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookieA));
        var segunda = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookieB));

        Assert.Equal(HttpStatusCode.Unauthorized, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
    }

    [Fact]
    public async Task TenantNoSePuedeFalsificarDesdeElCuerpo()
    {
        var (clienteId, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var (otroClienteId, _, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            Activacion(email, OrigenKiosco, clienteFalsificado: otroClienteId));
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var cookie = ExtraerCookie(respuesta);
        var token = cookie[(OpcionesKiosco.Cookie.Length + 1)..];
        var hash = CredencialKiosco.CalcularHash(token);

        await using var contexto = NuevoContexto(null, _factory.CadenaConexion);
        var sesion = await contexto.SesionesKiosco
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(s => s.HashCredencial == hash);

        Assert.Equal(clienteId, sesion.ClienteId);
        Assert.NotEqual(otroClienteId, sesion.ClienteId);
    }

    [Fact]
    public async Task TrabajadorNoPuedeActivarKiosco()
    {
        var (clienteId, _, tokenCliente) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var emailTrabajador = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Activacion(emailTrabajador, OrigenKiosco));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ClienteSinModuloNoPuedeActivarKiosco()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["GestionAvicola"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Activacion(email, OrigenKiosco));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task CredencialesInvalidasRespondenIgualQueCorreoInexistente()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var contrasenaMala = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email, contrasena = "contrasena-incorrecta" }),
        };
        contrasenaMala.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var correoInexistente = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email = "no-existe@icarus.test", contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        correoInexistente.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");

        var respuestaMala = await cliente.SendAsync(contrasenaMala);
        var respuestaCorreo = await cliente.SendAsync(correoInexistente);

        Assert.Equal(HttpStatusCode.Unauthorized, respuestaMala.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, respuestaCorreo.StatusCode);
    }

    [Fact]
    public async Task MutacionSinAntiforgeryEsRechazada()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            Activacion(email, OrigenKiosco, conAntiforgery: false));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task OrigenHermanoEsRechazado()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Activacion(email, OrigenHermano));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SalidaConReautenticacionRevocaLaSesion()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookie = await ActivarKioscoAsync(email);
        var cliente = _factory.CreateClient();

        var salida = new HttpRequestMessage(HttpMethod.Delete, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email, contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        salida.Headers.Add("Cookie", cookie);
        salida.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuestaSalida = await cliente.SendAsync(salida);
        Assert.Equal(HttpStatusCode.NoContent, respuestaSalida.StatusCode);

        var despues = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task SalidaConCredencialesDeOtroTenantNoRevoca()
    {
        var (_, emailA, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var (_, emailB, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookieA = await ActivarKioscoAsync(emailA);
        var cliente = _factory.CreateClient();

        var salida = new HttpRequestMessage(HttpMethod.Delete, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email = emailB, contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        salida.Headers.Add("Cookie", cookieA);
        salida.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuesta = await cliente.SendAsync(salida);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var sigue = await cliente.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookieA));
        Assert.Equal(HttpStatusCode.OK, sigue.StatusCode);
    }

    [Fact]
    public async Task SesionVencidaNoSeReactiva()
    {
        var (clienteId, _, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var token = CredencialKiosco.GenerarToken();
        var hash = CredencialKiosco.CalcularHash(token);
        var ahora = DateTimeOffset.UtcNow;

        await using (var contexto = NuevoContexto(clienteId, _factory.CadenaConexion))
        {
            contexto.SesionesKiosco.Add(new SesionKiosco(
                clienteId, hash, ahora.AddHours(-4), ahora.AddHours(-1)));
            await contexto.SaveChangesAsync();
        }

        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoKiosco(
            HttpMethod.Get, "/api/control-acceso/kiosco/sesion", $"{OpcionesKiosco.Cookie}={token}"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task SesionKioscoGuardada_SeRecuperaTrasReiniciarNavegador()
    {
        var (_, email, _) = await CrearClienteConModuloAsync(["ControlAcceso"]);
        var cookie = await ActivarKioscoAsync(email);

        // Un "reinicio" es un cliente nuevo con la misma cookie persistente.
        var trasReinicio = _factory.CreateClient();
        var respuesta = await trasReinicio.SendAsync(
            PedidoKiosco(HttpMethod.Get, "/api/control-acceso/kiosco/sesion", cookie));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private sealed class UsuarioDePrueba(Guid? clienteId) : ICurrentUser
    {
        public bool EstaAutenticado => clienteId.HasValue;
        public Guid? UsuarioId => null;
        public string? Rol => null;
        public Guid? ClienteId => clienteId;
        public Guid? TrabajadorId => null;
    }
}
