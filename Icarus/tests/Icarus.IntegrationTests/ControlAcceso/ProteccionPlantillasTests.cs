using System.Text;
using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Biometria;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class ProteccionPlantillasTests
{
    private readonly IdentityFactory _factory;
    private static readonly byte[] ClavePrueba = Convert.FromBase64String(
        "Irdz7YT9hZSPMdG0GRdbGJqLWBNmGPjtU87dHcDHqA0="); // 32 bytes

    public ProteccionPlantillasTests(IdentityFactory factory) => _factory = factory;

    private static ControlAccesoDbContext NuevoContexto(Guid? clienteId, string cadena)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioDePrueba(clienteId));
    }

    private static ProtectorPlantillas NuevoProtector() =>
        new(Options.Create(new OpcionesProteccionPlantillas
        {
            ClaveCifradoBase64 = Convert.ToBase64String(ClavePrueba),
        }));

    [Fact]
    public async Task PlantillaSePersisteCifradaYNoEsLegibleEnSql()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var vectorOriginal = Encoding.UTF8.GetBytes("vector-facial-sintetico-1");
        var protector = NuevoProtector();
        var protegida = protector.Proteger(
            vectorOriginal,
            "modelo-v1",
            clienteId,
            trabajadorId,
            versionEnrolamiento: 1,
            versionClave: 1);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        contexto.Set<PlantillaFacialProtegida>().Add(protegida);
        await contexto.SaveChangesAsync();

        var sqlCrudo = await contexto.Database.SqlQuery<string>(
            $"SELECT CAST(ContenidoCifrado AS VARCHAR(MAX)) AS Value FROM control_acceso.plantillas_faciales WHERE Id = {protegida.Id}").FirstAsync();

        Assert.DoesNotContain("vector-facial-sintetico", sqlCrudo);
    }

    [Fact]
    public async Task DescifrarConClaveIncorrecta_DevuelveNulo()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var vectorOriginal = Encoding.UTF8.GetBytes("vector-facial-sintetico-2");
        var protector = NuevoProtector();
        var protegida = protector.Proteger(
            vectorOriginal,
            "modelo-v1",
            clienteId,
            trabajadorId,
            versionEnrolamiento: 1,
            versionClave: 1);

        var otraClave = Encoding.UTF8.GetBytes("clave-incorrecta-32bytes-long");
        var recuperado = ProtectorPlantillas.Recuperar(protegida, otraClave);

        Assert.Null(recuperado);
    }

    [Fact]
    public async Task ManipularContenidoCifrado_DescifradoFalla()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var vectorOriginal = Encoding.UTF8.GetBytes("vector-facial-sintetico-3");
        var protector = NuevoProtector();
        var protegida = protector.Proteger(
            vectorOriginal,
            "modelo-v1",
            clienteId,
            trabajadorId,
            versionEnrolamiento: 1,
            versionClave: 1);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        contexto.Set<PlantillaFacialProtegida>().Add(protegida);
        await contexto.SaveChangesAsync();

        await contexto.Database.ExecuteSqlAsync(
            $"UPDATE control_acceso.plantillas_faciales SET ContenidoCifrado = 0x00 WHERE Id = {protegida.Id}");

        var manipulada = await contexto.Set<PlantillaFacialProtegida>().AsNoTracking()
            .FirstAsync(p => p.Id == protegida.Id);
        var recuperado = ProtectorPlantillas.Recuperar(manipulada, ClavePrueba);

        Assert.Null(recuperado);
    }

    [Fact]
    public async Task IntercambiarIdsEntreTenants_DescifradoFalla()
    {
        var clienteA = Guid.NewGuid();
        var clienteB = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var vectorOriginal = Encoding.UTF8.GetBytes("vector-facial-sintetico-4");
        var protector = NuevoProtector();
        var protegida = protector.Proteger(
            vectorOriginal,
            "modelo-v1",
            clienteA,
            trabajadorId,
            versionEnrolamiento: 1,
            versionClave: 1);

        await using var contexto = NuevoContexto(clienteA, _factory.CadenaConexion);
        contexto.Set<PlantillaFacialProtegida>().Add(protegida);
        await contexto.SaveChangesAsync();

        await contexto.Database.ExecuteSqlAsync(
            $"UPDATE control_acceso.plantillas_faciales SET ClienteId = {clienteB} WHERE Id = {protegida.Id}");

        var cambiada = await contexto.Set<PlantillaFacialProtegida>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(p => p.Id == protegida.Id);
        var recuperado = ProtectorPlantillas.Recuperar(cambiada, ClavePrueba);

        Assert.Null(recuperado);
    }

    [Fact]
    public async Task RevocarPlantillaActiva_ObtenerActivaDevuelveNulo()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var vectorOriginal = Encoding.UTF8.GetBytes("vector-facial-sintetico-5");
        var protector = NuevoProtector();
        var protegida = protector.Proteger(
            vectorOriginal,
            "modelo-v1",
            clienteId,
            trabajadorId,
            versionEnrolamiento: 1,
            versionClave: 1);

        await using var contexto = NuevoContexto(clienteId, _factory.CadenaConexion);
        contexto.Set<PlantillaFacialProtegida>().Add(protegida);
        await contexto.SaveChangesAsync();

        protegida.Revocar();
        await contexto.SaveChangesAsync();

        var activa = await contexto.Set<PlantillaFacialProtegida>()
            .AsNoTracking()
            .Where(p => p.ClienteId == clienteId && p.TrabajadorId == trabajadorId && p.EstaActivo)
            .FirstOrDefaultAsync();

        Assert.Null(activa);
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
