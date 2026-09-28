using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.ControlAcceso.Infrastructure.Repositorios;
using Icarus.Host.Endpoints;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class PersistenciaAccesoTests
{
    private readonly IdentityFactory _factory;

    public PersistenciaAccesoTests(IdentityFactory factory) => _factory = factory;

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

    private async Task<(Guid ClienteId, string TokenCliente)> CrearClienteConModuloAsync()
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var altaCliente = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        altaCliente.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Control Acceso S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaCliente = await clienteHttp.SendAsync(altaCliente);
        Assert.Equal(HttpStatusCode.Created, respuestaCliente.StatusCode);
        var clienteId = (await respuestaCliente.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = PedidoAutenticado(HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", admin);
        asignar.Content = JsonContent.Create(new { modulos = new[] { "ControlAcceso" } });
        Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);

        return (clienteId, await LoginComoAsync(email));
    }

    private async Task<Guid> CrearTrabajadorActivoAsync(Guid clienteId, string tokenCliente)
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
        return (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
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

    [Fact]
    public async Task GuardarYObtenerJornadaConMarcaciones()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var fecha = new DateOnly(2026, 9, 27);
        var cadena = _factory.CadenaConexion;

        await using var contexto = NuevoContexto(clienteId, cadena);
        var jornada = new JornadaAcceso(clienteId, trabajadorId, fecha);
        jornada.RegistrarMarcacion(
            TipoMarcacion.Entrada,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            fecha,
            Guid.NewGuid(),
            OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(
            TipoMarcacion.Salida,
            new DateTimeOffset(2026, 9, 27, 16, 0, 0, TimeSpan.Zero),
            fecha,
            Guid.NewGuid(),
            OrigenMarcacion.Kiosco);
        var repositorio = new RepositorioJornadasAcceso(contexto);
        repositorio.Agregar(jornada);
        await contexto.SaveChangesAsync();

        await using var contextoLectura = NuevoContexto(clienteId, cadena);
        var repositorioLectura = new RepositorioJornadasAcceso(contextoLectura);
        var recuperada = await repositorioLectura.ObtenerAsync(clienteId, trabajadorId, fecha);

        Assert.NotNull(recuperada);
        Assert.Equal(2, recuperada.Marcaciones.Count);
        Assert.Equal(2, recuperada.Version);
        Assert.Equal(TipoMarcacion.Entrada, recuperada.Marcaciones.First().Tipo);
        Assert.Equal(TipoMarcacion.Salida, recuperada.Marcaciones.Last().Tipo);
    }

    [Fact]
    public async Task DosJornadasMismaClave_ProvocanExcepcionDeUnicidad()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var fecha = new DateOnly(2026, 9, 27);
        var cadena = _factory.CadenaConexion;

        await using var contextoA = NuevoContexto(clienteId, cadena);
        await using var contextoB = NuevoContexto(clienteId, cadena);
        var repoA = new RepositorioJornadasAcceso(contextoA);
        var repoB = new RepositorioJornadasAcceso(contextoB);
        repoA.Agregar(new JornadaAcceso(clienteId, trabajadorId, fecha));
        repoB.Agregar(new JornadaAcceso(clienteId, trabajadorId, fecha));

        await contextoA.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<DbUpdateException>(() => contextoB.SaveChangesAsync());
        Assert.Contains("IX_", excepcion.InnerException?.Message ?? excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorreccionConcurrente_DetectaConflictoOptimista()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var fecha = new DateOnly(2026, 9, 27);
        var cadena = _factory.CadenaConexion;

        await using var contextoInicial = NuevoContexto(clienteId, cadena);
        var jornada = new JornadaAcceso(clienteId, trabajadorId, fecha);
        jornada.RegistrarMarcacion(
            TipoMarcacion.Entrada,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            fecha,
            Guid.NewGuid(),
            OrigenMarcacion.Kiosco);
        contextoInicial.Add(jornada);
        await contextoInicial.SaveChangesAsync();

        await using var contextoA = NuevoContexto(clienteId, cadena);
        await using var contextoB = NuevoContexto(clienteId, cadena);
        var repoA = new RepositorioJornadasAcceso(contextoA);
        var repoB = new RepositorioJornadasAcceso(contextoB);
        var enA = await repoA.ObtenerAsync(clienteId, trabajadorId, fecha);
        var enB = await repoB.ObtenerAsync(clienteId, trabajadorId, fecha);
        Assert.NotNull(enA);
        Assert.NotNull(enB);

        enA.Corregir(
            DateTimeOffset.UtcNow,
            fecha,
            enA.Version,
            "Corrección A",
            trabajadorId,
            (TipoMarcacion.Entrada, new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero)));
        enB.Corregir(
            DateTimeOffset.UtcNow,
            fecha,
            enB.Version,
            "Corrección B",
            trabajadorId,
            (TipoMarcacion.Entrada, new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero)));

        await contextoA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextoB.SaveChangesAsync());
    }

    [Fact]
    public async Task CorreccionSePersisteSinAlterarMarcacionesOriginales()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var fecha = new DateOnly(2026, 9, 27);
        var cadena = _factory.CadenaConexion;

        await using (var contextoCrear = NuevoContexto(clienteId, cadena))
        {
            var jornada = new JornadaAcceso(clienteId, trabajadorId, fecha);
            jornada.RegistrarMarcacion(
                TipoMarcacion.Entrada,
                new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
                fecha,
                Guid.NewGuid(),
                OrigenMarcacion.Kiosco);
            contextoCrear.Add(jornada);
            await contextoCrear.SaveChangesAsync();
        }

        await using var contexto = NuevoContexto(clienteId, cadena);
        var repo = new RepositorioJornadasAcceso(contexto);
        var cargada = await repo.ObtenerAsync(clienteId, trabajadorId, fecha);
        Assert.NotNull(cargada);
        cargada.Corregir(
            DateTimeOffset.UtcNow,
            fecha,
            cargada.Version,
            "Ajuste autorizado",
            trabajadorId,
            (TipoMarcacion.Entrada, new DateTimeOffset(2026, 9, 27, 11, 30, 0, TimeSpan.Zero)));
        await contexto.SaveChangesAsync();

        await using var contextoLectura = NuevoContexto(clienteId, cadena);
        var repoLectura = new RepositorioJornadasAcceso(contextoLectura);
        var recuperada = await repoLectura.ObtenerAsync(clienteId, trabajadorId, fecha);

        Assert.NotNull(recuperada);
        Assert.Single(recuperada.Marcaciones);
        Assert.Single(recuperada.Revisiones);
        Assert.Equal(new TimeSpan(11, 30, 0), recuperada.ValoresEfectivos(fecha).First().InstanteUtc.TimeOfDay);
    }

    [Fact]
    public async Task FiltroTenant_NoDevuelveJornadasAjenas()
    {
        var (clienteA, tokenA) = await CrearClienteConModuloAsync();
        var trabajadorA = await CrearTrabajadorActivoAsync(clienteA, tokenA);
        var (clienteB, _) = await CrearClienteConModuloAsync();
        var fecha = new DateOnly(2026, 9, 27);
        var cadena = _factory.CadenaConexion;

        await using var contextoA = NuevoContexto(clienteA, cadena);
        contextoA.Add(new JornadaAcceso(clienteA, trabajadorA, fecha));
        await contextoA.SaveChangesAsync();

        await using var contextoB = NuevoContexto(clienteB, cadena);
        var repoB = new RepositorioJornadasAcceso(contextoB);
        var ajena = await repoB.ObtenerAsync(clienteA, trabajadorA, fecha);

        Assert.Null(ajena);
    }

    [Fact]
    public async Task ListarJornadas_RespetaRangoYPaginacion()
    {
        var (clienteId, tokenCliente) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorActivoAsync(clienteId, tokenCliente);
        var cadena = _factory.CadenaConexion;

        await using var contexto = NuevoContexto(clienteId, cadena);
        var repo = new RepositorioJornadasAcceso(contexto);
        repo.Agregar(new JornadaAcceso(clienteId, trabajadorId, new DateOnly(2026, 9, 25)));
        repo.Agregar(new JornadaAcceso(clienteId, trabajadorId, new DateOnly(2026, 9, 26)));
        repo.Agregar(new JornadaAcceso(clienteId, trabajadorId, new DateOnly(2026, 9, 27)));
        await contexto.SaveChangesAsync();

        var pagina = await repo.ListarAsync(
            clienteId,
            trabajadorId,
            new DateOnly(2026, 9, 26),
            new DateOnly(2026, 9, 27),
            new PeticionPaginada(1, 10));

        Assert.Equal(2, pagina.Items.Count);
        Assert.Equal(2, pagina.Total);
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
