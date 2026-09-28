using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class EnrolamientoTests
{
    private readonly IdentityFactory _factory;

    public EnrolamientoTests(IdentityFactory factory) => _factory = factory;

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

    private async Task<(Guid ClienteId, string Token)> CrearClienteConModuloAsync()
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Enrolamiento S.A.C.",
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

    private async Task<JsonElement> EnrolarAsync(
        string token, Guid trabajadorId, string muestra, Guid? clave = null)
    {
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/enrolamiento", token);
        pedido.Content = JsonContent.Create(new
        {
            muestraBase64 = Base64(muestra),
            formato = "jpg",
            claveIdempotencia = clave ?? Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task EnrolamientoExitosoHabilitaYPersistePlantillaCifrada()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        var resultado = await EnrolarAsync(token, trabajadorId, "rostro-sintetico-1");

        Assert.True(resultado.GetProperty("exitoso").GetBoolean());
        Assert.Equal(1, resultado.GetProperty("versionEnrolamiento").GetInt32());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var config = await contexto.ConfiguracionAccesoTrabajador
            .SingleAsync(c => c.TrabajadorId == trabajadorId);
        Assert.True(config.Habilitado);
        Assert.Equal(EstadoEnrolamiento.Vigente, config.Enrolamiento);
        Assert.Equal(1, config.VersionEnrolamiento);

        var plantilla = await contexto.PlantillasFaciales
            .SingleAsync(p => p.TrabajadorId == trabajadorId && p.EstaActivo);
        Assert.DoesNotContain("rostro-sintetico-1", Encoding.ASCII.GetString(plantilla.ContenidoCifrado));
    }

    [Fact]
    public async Task EnrolamientoConPadFallidoNoHabilitaNiGuardaPlantilla()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        var resultado = await EnrolarAsync(token, trabajadorId, "PAD_FALLA");

        Assert.False(resultado.GetProperty("exitoso").GetBoolean());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        Assert.False(await contexto.PlantillasFaciales
            .AnyAsync(p => p.TrabajadorId == trabajadorId));
        var config = await contexto.ConfiguracionAccesoTrabajador
            .FirstOrDefaultAsync(c => c.TrabajadorId == trabajadorId);
        Assert.True(config is null || !config.Habilitado);
    }

    [Fact]
    public async Task ReintentoConMismaClaveNoDuplicaPlantillaNiCambiaLaVersion()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var clave = Guid.NewGuid();

        var primera = await EnrolarAsync(token, trabajadorId, "rostro-1", clave);
        var segunda = await EnrolarAsync(token, trabajadorId, "rostro-1", clave);

        Assert.Equal(
            primera.GetProperty("versionEnrolamiento").GetInt32(),
            segunda.GetProperty("versionEnrolamiento").GetInt32());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var activas = await contexto.PlantillasFaciales
            .CountAsync(p => p.TrabajadorId == trabajadorId && p.EstaActivo);
        Assert.Equal(1, activas);
    }

    [Fact]
    public async Task SustituirRostroIncrementaVersionYRevocaLaAnterior()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);

        await EnrolarAsync(token, trabajadorId, "rostro-1");
        var segunda = await EnrolarAsync(token, trabajadorId, "rostro-2");

        Assert.Equal(2, segunda.GetProperty("versionEnrolamiento").GetInt32());

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var todas = await contexto.PlantillasFaciales
            .Where(p => p.TrabajadorId == trabajadorId)
            .ToListAsync();
        Assert.Equal(2, todas.Count);
        Assert.Single(todas, p => p.EstaActivo);
        Assert.Equal(2, todas.Single(p => p.EstaActivo).VersionEnrolamiento);
    }

    [Fact]
    public async Task RevocarDeshabilitaYEliminaLaPlantillaActiva()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");

        var cliente = _factory.CreateClient();
        var revocar = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/revocacion", token));
        Assert.Equal(HttpStatusCode.NoContent, revocar.StatusCode);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        var config = await contexto.ConfiguracionAccesoTrabajador
            .SingleAsync(c => c.TrabajadorId == trabajadorId);
        Assert.False(config.Habilitado);
        Assert.Equal(EstadoEnrolamiento.Revocado, config.Enrolamiento);
        Assert.False(await contexto.PlantillasFaciales
            .AnyAsync(p => p.TrabajadorId == trabajadorId && p.EstaActivo));
    }

    [Fact]
    public async Task DeshabilitarEsReversibleYConservaLaPlantilla()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");

        var cliente = _factory.CreateClient();
        var deshabilitar = PedidoAutenticado(
            HttpMethod.Put, $"/api/control-acceso/trabajadores/{trabajadorId}/habilitacion", token);
        deshabilitar.Content = JsonContent.Create(new { habilitado = false });
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(deshabilitar)).StatusCode);

        await using (var contexto = NuevoContexto(clienteId, _factory.CadenaConexion))
        {
            var config = await contexto.ConfiguracionAccesoTrabajador
                .SingleAsync(c => c.TrabajadorId == trabajadorId);
            Assert.False(config.Habilitado);
            Assert.True(await contexto.PlantillasFaciales
                .AnyAsync(p => p.TrabajadorId == trabajadorId && p.EstaActivo));
        }

        var habilitar = PedidoAutenticado(
            HttpMethod.Put, $"/api/control-acceso/trabajadores/{trabajadorId}/habilitacion", token);
        habilitar.Content = JsonContent.Create(new { habilitado = true });
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(habilitar)).StatusCode);

        await using (var contexto = NuevoContexto(clienteId, _factory.CadenaConexion))
        {
            var config = await contexto.ConfiguracionAccesoTrabajador
                .SingleAsync(c => c.TrabajadorId == trabajadorId);
            Assert.True(config.Habilitado);
        }
    }

    [Fact]
    public async Task TrabajadorDeOtroTenantNoSePuedeEnrolar()
    {
        var (clienteA, tokenA) = await CrearClienteConModuloAsync();
        var trabajadorA = await CrearTrabajadorAsync(clienteA, tokenA);
        var (_, tokenB) = await CrearClienteConModuloAsync();

        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post,
            $"/api/control-acceso/trabajadores/{trabajadorA}/enrolamiento", tokenB);
        pedido.Content = JsonContent.Create(new
        {
            muestraBase64 = Base64("rostro-1"),
            formato = "jpg",
            claveIdempotencia = Guid.NewGuid(),
        });

        var respuesta = await cliente.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ListarAccesoDevuelveLaConfiguracionDelTenant()
    {
        var (clienteId, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarAsync(token, trabajadorId, "rostro-1");

        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/api/control-acceso/trabajadores/acceso", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var item = cuerpo.EnumerateArray()
            .Single(x => x.GetProperty("trabajadorId").GetGuid() == trabajadorId);
        Assert.True(item.GetProperty("habilitado").GetBoolean());
        Assert.Equal("Vigente", item.GetProperty("enrolamiento").GetString());
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
