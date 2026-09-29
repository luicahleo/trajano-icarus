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
public sealed class IntentosMarcacionTests
{
    private const int MaximoIntentos = 3;

    private readonly IdentityFactory _factory;

    public IntentosMarcacionTests(IdentityFactory factory) => _factory = factory;

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
            razonSocial = "Cliente Intentos S.A.C.",
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

    [Fact]
    public async Task TresRechazosDefinitivosCreanUnaSolaIncidenciaYSinMarcaciones()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        Console.WriteLine($"PRIMERA: {primera}");
        Assert.Equal("Rechazada", primera.GetProperty("estado").GetString());
        Assert.Equal(1, primera.GetProperty("intentos").GetInt32());
        var flujoId = primera.GetProperty("flujoId").GetGuid();

        var (segunda, _) = await CapturarAsync(cookie, "Entrada", "VARIOS_ROSTROS", flujoId);
        Console.WriteLine($"SEGUNDA: {segunda}");
        Assert.Equal("Rechazada", segunda.GetProperty("estado").GetString());
        Assert.Equal(2, segunda.GetProperty("intentos").GetInt32());

        var (tercera, _) = await CapturarAsync(cookie, "Entrada", "PAD_FALLA", flujoId);
        Assert.Equal("Incidencia", tercera.GetProperty("estado").GetString());
        Assert.Equal(MaximoIntentos, tercera.GetProperty("intentos").GetInt32());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.Single(contexto.IncidenciasAcceso);
        Assert.Empty(contexto.JornadasAcceso.Where(j => j.TrabajadorId == trabajadorId));
    }

    [Fact]
    public async Task ExitoEnSegundoIntentoRegistraLaMarcacionYSinIncidencia()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        Assert.Equal("Rechazada", primera.GetProperty("estado").GetString());
        var flujoId = primera.GetProperty("flujoId").GetGuid();

        var (segunda, _) = await CapturarAsync(cookie, "Entrada", "rostro-1", flujoId);
        Assert.Equal("Registrada", segunda.GetProperty("estado").GetString());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.Empty(contexto.IncidenciasAcceso);
        var jornada = await contexto.JornadasAcceso
            .Include(j => j.Marcaciones)
            .SingleAsync(j => j.TrabajadorId == trabajadorId);
        Assert.Single(jornada.Marcaciones);
    }

    [Fact]
    public async Task ReenviarMismaClaveNoRepiteProveedorNiSumaIntento()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var clave = Guid.NewGuid();
        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO", clave: clave);
        var flujoId = primera.GetProperty("flujoId").GetGuid();
        Assert.Equal(1, primera.GetProperty("intentos").GetInt32());

        var (reintento, _) = await CapturarAsync(cookie, "Entrada", "rostro-1", flujoId, clave);
        Assert.Equal(1, reintento.GetProperty("intentos").GetInt32());
        Assert.Equal("Rechazada", reintento.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task CambiarAccionEnElMismoFlujoEsRechazado()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        var flujoId = primera.GetProperty("flujoId").GetGuid();

        var (otra, _) = await CapturarAsync(cookie, "Salida", "SIN_ROSTRO", flujoId);
        Assert.Equal("Rechazada", otra.GetProperty("estado").GetString());
        Assert.Equal("accion_incompatible", otra.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task ConcurrenciaSobreElMismoFlujoNoSuperaTresIntentosNiDuplicaIncidencia()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (primera, _) = await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO");
        var flujoId = primera.GetProperty("flujoId").GetGuid();
        await CapturarAsync(cookie, "Entrada", "SIN_ROSTRO", flujoId);

        var claves = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var tareas = claves.Select(c => CapturarAsync(cookie, "Entrada", "SIN_ROSTRO", flujoId, c))
            .ToList();
        var resultados = await Task.WhenAll(tareas);

        var incidencias = resultados.Count(r => r.Respuesta.GetProperty("estado").GetString() == "Incidencia");
        var conflictos = resultados.Count(r => r.Respuesta.GetProperty("estado").GetString() == "Rechazada"
            && r.Respuesta.GetProperty("motivo").GetString() == "conflicto");
        var rechazos = resultados.Count(r => r.Respuesta.GetProperty("estado").GetString() == "Rechazada"
            && r.Respuesta.GetProperty("motivo").GetString() != "conflicto");

        Assert.Equal(1, incidencias);
        Assert.True(rechazos + conflictos == 3,
            $"Esperado 3 no-incidencias, obtuvo {rechazos + conflictos} (rechazos={rechazos}, conflictos={conflictos})");

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.Single(contexto.IncidenciasAcceso);
    }

    [Fact]
    public async Task SalidaSinEntradaAbiertaNoConsumeIntentoFacial()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var cookie = await ActivarKioscoAsync(email);

        var (resultado, _) = await CapturarAsync(cookie, "Salida", "rostro-1");
        Assert.Equal("Rechazada", resultado.GetProperty("estado").GetString());
        Assert.Equal("salida_sin_entrada", resultado.GetProperty("motivo").GetString());
        Assert.False(resultado.TryGetProperty("flujoId", out _));

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.Empty(contexto.IncidenciasAcceso);
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
