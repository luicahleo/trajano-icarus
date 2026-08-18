using Icarus.GestionAvicola.Application.Eficiencia;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;
public class ObtenerEficienciaGalponHandlerTests
{
    [Fact]
    public async Task UsaSnapshotDelUltimoEventoYExcluyeDescarte()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow); var g = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 2970, hoy.AddDays(-1), null); var gs = Substitute.For<IRepositorioGalpones>(); var ps = Substitute.For<IRepositorioProduccion>(); var ms = Substitute.For<IRepositorioMortalidad>(); gs.ObtenerPorIdAsync(g.Id, Arg.Any<CancellationToken>()).Returns(g);
        ps.ListarPorRangoAsync(g.Id, hoy, hoy, Arg.Any<CancellationToken>()).Returns(new List<RegistroProduccion> { new(g.Id, g.ClienteId, hoy, new(10, 0), 80, 0, 2, 5, 3000, null) }); ms.ListarPorRangoAsync(g.Id, hoy, hoy, Arg.Any<CancellationToken>()).Returns(new List<RegistroMortalidad> { new(g.Id, g.ClienteId, hoy, new(18, 0), 30, 2970, null) });
        var d = Assert.Single((await new ObtenerEficienciaGalponHandler(gs, ps, ms).Handle(new(g.Id, hoy, hoy), CancellationToken.None)).Dias); Assert.Equal(2400, d.TotalVendible); Assert.Equal(65, d.TotalDescarte); Assert.Equal(80.81m, d.Eficiencia);
    }
}
