using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class MarcacionesManualesTests
{
    // Bolivia no tiene horario de verano: se fija el desfase y se usan horas
    // del día anterior para que la marcación siempre sea pasada y del mismo día.
    private static readonly TimeSpan OffsetBolivia = TimeSpan.FromHours(-4);

    private readonly IdentityFactory _factory;

    public MarcacionesManualesTests(IdentityFactory factory) => _factory = factory;

    private static ControlAccesoDbContext NuevoContexto(Guid? clienteId, string cadena)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioDePrueba(clienteId));
    }

    private static DateOnly AyerBolivia() =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(OffsetBolivia).DateTime).AddDays(-1);

    private static DateTimeOffset HoraBolivia(DateOnly fecha, int hora) =>
        new(fecha.ToDateTime(new TimeOnly(hora, 0)), OffsetBolivia);

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

    private async Task<(Guid ClienteId, string Token)> CrearClienteConModuloAsync()
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Manual S.A.C.",
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

        return (clienteId, await LoginComoAsync(email));
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

    private Task<HttpResponseMessage> RegistrarAsync(
        string token, Guid trabajadorId, string tipo, DateTimeOffset horaDeclarada,
        string motivo, Guid clave)
    {
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, "/api/control-acceso/marcaciones/manuales", token);
        pedido.Content = JsonContent.Create(new
        {
            trabajadorId,
            tipo,
            horaDeclaradaUtc = horaDeclarada,
            motivo,
            claveIdempotencia = clave,
        });
        return cliente.SendAsync(pedido);
    }

    private async Task<Guid> RegistrarCreadaAsync(
        string token, Guid trabajadorId, string tipo, DateTimeOffset horaDeclarada,
        string motivo = "Falla de reconocimiento", Guid? clave = null)
    {
        var respuesta = await RegistrarAsync(
            token, trabajadorId, tipo, horaDeclarada, motivo, clave ?? Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jornadaId").GetGuid();
    }

    private async Task<JsonElement> ObtenerJornadaAsync(string token, Guid jornadaId)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/api/control-acceso/jornadas/{jornadaId}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task EntradaYSalidaManualQuedanValidasYSonManuales()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var fecha = AyerBolivia();

        var jornadaId = await RegistrarCreadaAsync(token, trabajadorId, "Entrada", HoraBolivia(fecha, 8));
        await RegistrarCreadaAsync(token, trabajadorId, "Salida", HoraBolivia(fecha, 12), clave: Guid.NewGuid());

        var detalle = await ObtenerJornadaAsync(token, jornadaId);
        var marcaciones = detalle.GetProperty("marcaciones").EnumerateArray().ToList();
        Assert.Equal(2, marcaciones.Count);
        Assert.All(marcaciones, m =>
        {
            Assert.Equal("ManualCliente", m.GetProperty("origen").GetString());
            Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("motivo").GetString()));
        });
        Assert.Equal("Completa", detalle.GetProperty("estado").GetString());

        // La hora declarada y el instante real de creación son datos distintos.
        var primera = marcaciones[0];
        Assert.True(primera.GetProperty("creadaEnUtc").GetDateTimeOffset() >
                    primera.GetProperty("horaDeclaradaUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task MotivoVacioEsRechazado()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        var respuesta = await RegistrarAsync(
            token, trabajadorId, "Entrada", HoraBolivia(AyerBolivia(), 8), "  ", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task HoraFuturaEsRechazada()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        var respuesta = await RegistrarAsync(
            token, trabajadorId, "Entrada", DateTimeOffset.UtcNow.AddDays(1),
            "Futuro", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorDeOtroTenantEsRechazado()
    {
        var (clienteA, tokenA) = await CrearClienteConModuloAsync();
        var trabajadorA = await CrearTrabajadorAsync(clienteA, tokenA);
        var (_, tokenB) = await CrearClienteConModuloAsync();

        var respuesta = await RegistrarAsync(
            tokenB, trabajadorA, "Entrada", HoraBolivia(AyerBolivia(), 8), "Ajeno", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ReintentoConMismaClaveNoDuplica()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var clave = Guid.NewGuid();
        var hora = HoraBolivia(AyerBolivia(), 8);

        var primera = await RegistrarCreadaAsync(token, trabajadorId, "Entrada", hora, clave: clave);
        var segunda = await RegistrarCreadaAsync(token, trabajadorId, "Entrada", hora, clave: clave);

        Assert.Equal(primera, segunda);
        var detalle = await ObtenerJornadaAsync(token, primera);
        Assert.Single(detalle.GetProperty("marcaciones").EnumerateArray());
    }

    [Fact]
    public async Task SalidaSinEntradaEsRechazada()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        var respuesta = await RegistrarAsync(
            token, trabajadorId, "Salida", HoraBolivia(AyerBolivia(), 12), "Salida suelta", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task DosRegistrosConcurrentesConMismaClaveNoDuplican()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var clave = Guid.NewGuid();
        var hora = HoraBolivia(AyerBolivia(), 8);
        var fecha = AyerBolivia();

        var respuestas = await Task.WhenAll(
            RegistrarAsync(token, trabajadorId, "Entrada", hora, "Carrera", clave),
            RegistrarAsync(token, trabajadorId, "Entrada", hora, "Carrera", clave));

        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.DoesNotContain(respuestas, r => r.StatusCode == HttpStatusCode.InternalServerError);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var jornadas = await contexto.JornadasAcceso
            .Include(j => j.Marcaciones)
            .Where(j => j.TrabajadorId == trabajadorId && j.FechaBoliviana == fecha)
            .ToListAsync();
        Assert.Single(jornadas);
        Assert.Single(jornadas[0].Marcaciones);
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
