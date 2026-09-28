using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class MarcacionesTests
{
    private readonly IdentityFactory _factory;

    public MarcacionesTests(IdentityFactory factory) => _factory = factory;

    private static ControlAccesoDbContext NuevoContexto(Guid? clienteId, string cadena)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioDePrueba(clienteId));
    }

    private static string Base64(string contenido) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(contenido));

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private async Task<string> LoginComoAsync(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
    }

    private async Task<(Guid ClienteId, string Email, string Token)> CrearClienteConModuloAsync()
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Marcaciones S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await clienteHttp.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var clienteId = (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = PedidoAutenticado(HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", admin);
        asignar.Content = JsonContent.Create(new { modulos = new[] { "ControlAcceso" } });
        Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);

        return (clienteId, email, await LoginComoAsync(email));
    }

    private async Task<Guid> CrearTrabajadorAsync(Guid clienteId, string tokenCliente)
    {
        var cliente = _factory.CreateClient();
        var email = $"ta-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", tokenCliente);
        alta.Content = JsonContent.Create(new
        {
            nombre = "Nombre Ficticio",
            documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await cliente.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task EnrolarAsync(string token, Guid trabajadorId, string muestra)
    {
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/enrolamiento", token);
        pedido.Content = JsonContent.Create(new
        {
            muestraBase64 = Base64(muestra),
            formato = "jpg",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("exitoso").GetBoolean());
    }

    private async Task<string> ActivarKioscoAsync(string email)
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email, contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var setCookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(OpcionesKiosco.Cookie + "=", StringComparison.Ordinal));
        return setCookie.Split(';')[0];
    }

    private async Task<JsonElement> MarcarAsync(
        string cookie, string accion, string muestra, Guid? clave = null)
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/marcaciones")
        {
            Content = JsonContent.Create(new
            {
                accion,
                muestraBase64 = Base64(muestra),
                formato = "jpg",
                claveIdempotencia = clave ?? Guid.NewGuid(),
            }),
        };
        pedido.Headers.Add("Cookie", cookie);
        pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ConfirmarSalidaAsync(string cookie, Guid propuestaId)
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(
            HttpMethod.Post, $"/api/control-acceso/kiosco/marcaciones/{propuestaId}/confirmar");
        pedido.Headers.Add("Cookie", cookie);
        pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task EntradaYSalidaExitosasRegistranLaJornada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var entrada = await MarcarAsync(cookie, "Entrada", "rostro-1");
        var salida = await MarcarAsync(cookie, "Salida", "rostro-1");

        Assert.Equal("Registrada", entrada.GetProperty("estado").GetString());
        Assert.Equal("Entrada", entrada.GetProperty("tipo").GetString());
        Assert.Equal("Nombre Ficticio", entrada.GetProperty("nombreCompleto").GetString());
        Assert.Equal("Registrada", salida.GetProperty("estado").GetString());
        Assert.Equal("Salida", salida.GetProperty("tipo").GetString());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var jornada = await contexto.JornadasAcceso
            .Include(j => j.Marcaciones)
            .SingleAsync(j => j.TrabajadorId == trabajadorId);
        Assert.Equal(2, jornada.Marcaciones.Count);
    }

    [Fact]
    public async Task RostroNoReconocidoDevuelveRechazoGenerico()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var resultado = await MarcarAsync(cookie, "Entrada", "otro-rostro");

        Assert.Equal("Rechazada", resultado.GetProperty("estado").GetString());
        Assert.Equal("sin_coincidencia", resultado.GetProperty("motivo").GetString());
        Assert.Equal(JsonValueKind.Null, resultado.GetProperty("trabajadorId").ValueKind);
    }

    [Fact]
    public async Task PruebaDeVidaFallidaEsRechazada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var resultado = await MarcarAsync(cookie, "Entrada", "PAD_FALLA");

        Assert.Equal("Rechazada", resultado.GetProperty("estado").GetString());
        Assert.Equal("pad_fallido", resultado.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task SalidaSinEntradaAbiertaEsRechazada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var resultado = await MarcarAsync(cookie, "Salida", "rostro-1");

        Assert.Equal("Rechazada", resultado.GetProperty("estado").GetString());
        Assert.Equal("salida_sin_entrada", resultado.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task EntradaConEntradaAbiertaProponeSalidaYLaConfirma()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await MarcarAsync(cookie, "Entrada", "rostro-1");

        var propuesta = await MarcarAsync(cookie, "Entrada", "rostro-1");
        Assert.Equal("PropuestaSalida", propuesta.GetProperty("estado").GetString());
        var propuestaId = propuesta.GetProperty("propuestaId").GetGuid();

        var confirmacion = await ConfirmarSalidaAsync(cookie, propuestaId);
        Assert.Equal("Registrada", confirmacion.GetProperty("estado").GetString());
        Assert.Equal("Salida", confirmacion.GetProperty("tipo").GetString());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var jornada = await contexto.JornadasAcceso
            .Include(j => j.Marcaciones)
            .SingleAsync(j => j.TrabajadorId == trabajadorId);
        Assert.Equal(2, jornada.Marcaciones.Count);
        Assert.Equal(TipoMarcacion.Salida, jornada.Marcaciones.OrderBy(m => m.InstanteUtc).Last().Tipo);
    }

    [Fact]
    public async Task ReintentoConMismaClaveNoDuplicaLaMarcacion()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        var clave = Guid.NewGuid();

        var primera = await MarcarAsync(cookie, "Entrada", "rostro-1", clave);
        var segunda = await MarcarAsync(cookie, "Entrada", "rostro-1", clave);

        Assert.Equal("Registrada", primera.GetProperty("estado").GetString());
        Assert.Equal("Registrada", segunda.GetProperty("estado").GetString());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var jornada = await contexto.JornadasAcceso
            .Include(j => j.Marcaciones)
            .SingleAsync(j => j.TrabajadorId == trabajadorId);
        Assert.Single(jornada.Marcaciones);
    }

    [Fact]
    public async Task MismaClaveConOtraAccionEsRechazada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        var clave = Guid.NewGuid();

        await MarcarAsync(cookie, "Entrada", "rostro-1", clave);
        var otraAccion = await MarcarAsync(cookie, "Salida", "rostro-1", clave);

        Assert.Equal("Rechazada", otraAccion.GetProperty("estado").GetString());
        Assert.Equal("clave_reutilizada", otraAccion.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task PlantillaRevocadaNoPermiteMarcar()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");

        var cliente = _factory.CreateClient();
        var revocar = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/revocacion", token));
        Assert.Equal(HttpStatusCode.NoContent, revocar.StatusCode);

        var cookie = await ActivarKioscoAsync(email);
        var resultado = await MarcarAsync(cookie, "Entrada", "rostro-1");

        Assert.Equal("Rechazada", resultado.GetProperty("estado").GetString());
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
