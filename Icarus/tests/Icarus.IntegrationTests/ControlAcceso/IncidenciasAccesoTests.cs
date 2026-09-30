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
public sealed class IncidenciasAccesoTests
{
    private readonly IdentityFactory _factory;

    public IncidenciasAccesoTests(IdentityFactory factory) => _factory = factory;

    private static ControlAccesoDbContext NuevoContexto(Guid? clienteId, string cadena)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioDePrueba(clienteId));
    }

    private static string Base64(string contenido) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(contenido));

    private static HttpRequestMessage PedidoAutenticado(
        HttpMethod metodo, string url, string token, HttpContent? contenido = null) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = contenido,
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
            razonSocial = "Cliente Incidencias S.A.C.",
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

    private async Task<(JsonElement Respuesta, Guid Clave)> CapturarAsync(
        string cookie, string accion, string muestra, Guid? flujoId = null, Guid? clave = null)
    {
        var claveCaptura = clave ?? Guid.NewGuid();
        var cliente = _factory.CreateClient();
        var cuerpo = new Dictionary<string, object>
        {
            ["accion"] = accion,
            ["muestraBase64"] = Base64(muestra),
            ["formato"] = "jpg",
            ["claveIdempotencia"] = claveCaptura,
        };
        if (flujoId.HasValue)
            cuerpo["flujoId"] = flujoId.Value;

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/marcaciones")
        {
            Content = JsonContent.Create(cuerpo),
        };
        pedido.Headers.Add("Cookie", cookie);
        pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>(), claveCaptura);
    }

    private async Task<Guid> CrearIncidenciaAsync(string cookie)
    {
        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        var flujoId = primera.GetProperty("flujoId").GetGuid();
        await CapturarAsync(cookie, "Entrada", "VARIOS_ROSTROS", flujoId);
        var (tercera, _) = await CapturarAsync(cookie, "Entrada", "PAD_FALLA", flujoId);
        Assert.Equal("Incidencia", tercera.GetProperty("estado").GetString());
        return tercera.GetProperty("flujoId").GetGuid();
    }

    [Fact]
    public async Task TresRechazosCreanUnaNotificacionInternaParaElCliente()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = Assert.Single(contexto.IncidenciasAcceso);
        var notificacion = Assert.Single(contexto.NotificacionesInternasAcceso);
        Assert.Equal(incidencia.Id, notificacion.IncidenciaId);
        Assert.Equal(clienteId, notificacion.ClienteId);
        Assert.False(notificacion.Leida);
    }

    [Fact]
    public async Task ReenviarTercerRechazoNoDuplicaLaNotificacion()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (primera, clave) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        var flujoId = primera.GetProperty("flujoId").GetGuid();
        await CapturarAsync(cookie, "Entrada", "VARIOS_ROSTROS", flujoId);
        await CapturarAsync(cookie, "Entrada", "PAD_FALLA", flujoId);
        await CapturarAsync(cookie, "Entrada", "PAD_FALLA", flujoId, clave);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.Single(contexto.IncidenciasAcceso);
        Assert.Single(contexto.NotificacionesInternasAcceso);
    }

    [Fact]
    public async Task ClienteAjenoNoVeNiListaNiDetalleDeIncidencia()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = await contexto.IncidenciasAcceso.SingleAsync();

        var (otroClienteId, _, otroToken) = await CrearClienteConModuloAsync();
        _ = otroClienteId;
        var cliente = _factory.CreateClient();

        var lista = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/api/control-acceso/incidencias", otroToken));
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var items = (await lista.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Empty(items);

        var detalle = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/api/control-acceso/incidencias/{incidencia.Id}", otroToken));
        Assert.Equal(HttpStatusCode.NotFound, detalle.StatusCode);
    }

    [Fact]
    public async Task ResolucionSinMotivoEsRechazada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = await contexto.IncidenciasAcceso.SingleAsync();

        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/incidencias/{incidencia.Id}/resolucion", token);
        pedido.Content = JsonContent.Create(new
        {
            trabajadorId,
            tipo = "Entrada",
            horaDeclaradaUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            motivo = "   ",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        await using var contexto2 = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidenciaLuego = await contexto2.IncidenciasAcceso.SingleAsync();
        Assert.Equal(EstadoIncidenciaAcceso.Pendiente, incidenciaLuego.Estado);
    }

    [Fact]
    public async Task ResolucionConTrabajadorAjenoNoCierraLaIncidencia()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = await contexto.IncidenciasAcceso.SingleAsync();

        var (otroClienteId, _, otroToken) = await CrearClienteConModuloAsync();
        var otroTrabajadorId = await CrearTrabajadorAsync(otroClienteId, otroToken);

        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/incidencias/{incidencia.Id}/resolucion", token);
        pedido.Content = JsonContent.Create(new
        {
            trabajadorId = otroTrabajadorId,
            tipo = "Entrada",
            horaDeclaradaUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            motivo = "Corrección por incidencia",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        await using var contexto2 = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidenciaLuego = await contexto2.IncidenciasAcceso.SingleAsync();
        Assert.Equal(EstadoIncidenciaAcceso.Pendiente, incidenciaLuego.Estado);
    }

    [Fact]
    public async Task DescarteNoCreaMarcacionYDejaIncidenciaDescartada()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = await contexto.IncidenciasAcceso.SingleAsync();

        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/incidencias/{incidencia.Id}/descarte", token);
        pedido.Content = JsonContent.Create(new
        {
            motivo = "No correspondía una marcación",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        await using var contexto2 = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidenciaLuego = await contexto2.IncidenciasAcceso.SingleAsync();
        Assert.Equal(EstadoIncidenciaAcceso.Descartada, incidenciaLuego.Estado);
        Assert.Empty(contexto2.JornadasAcceso);
    }

    [Fact]
    public async Task DobleResolucionConMismaClaveEsIdempotente()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var incidencia = await contexto.IncidenciasAcceso.SingleAsync();

        var cliente = _factory.CreateClient();
        var clave = Guid.NewGuid();
        var hora = DateTimeOffset.UtcNow.AddMinutes(-1);
        var url = $"/api/control-acceso/incidencias/{incidencia.Id}/resolucion";
        var cuerpo = new
        {
            trabajadorId,
            tipo = "Entrada",
            horaDeclaradaUtc = hora,
            motivo = "Corrección por incidencia",
            claveIdempotencia = clave,
        };

        var primera = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Post, url, token, JsonContent.Create(cuerpo)));
        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);

        var segunda = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Post, url, token, JsonContent.Create(cuerpo)));
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);

        await using var contexto2 = NuevoContexto(clienteId, _factory.CadenaConexion);
        var jornada = await contexto2.JornadasAcceso
            .Include(j => j.Marcaciones)
            .SingleAsync(j => j.TrabajadorId == trabajadorId);
        Assert.Single(jornada.Marcaciones);
    }

    [Fact]
    public async Task ListarIncidenciasMuestraLaIncidenciaPendienteDelCliente()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);
        await CrearIncidenciaAsync(cookie);

        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/api/control-acceso/incidencias", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var items = cuerpo.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("Pendiente", items[0].GetProperty("estado").GetString());
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
