using Icarus.BuildingBlocks.Application.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;
public class RegistrarMortalidadHandlerTests
{
    [Fact]
    public async Task RegistraDescontandoYNarrando()
    {
        var g = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        var gs = Substitute.For<IRepositorioGalpones>(); var ms = Substitute.For<IRepositorioMortalidad>(); var vuelo = Substitute.For<IRegistroVuelo>(); var u = Substitute.For<IUnidadTrabajoGestionAvicola>(); gs.ObtenerPorIdAsync(g.Id, Arg.Any<CancellationToken>()).Returns(g);
        await new RegistrarMortalidadHandler(gs, ms, vuelo, u).Handle(new(g.Id, null, 15, null), CancellationToken.None);
        Assert.Equal(4785, g.GallinasActuales); ms.Received().Agregar(Arg.Is<RegistroMortalidad>(x => x.GallinasVivas == 4785)); vuelo.Received().Decidir("avicola.mortalidad.registrar", "ajuste_inventario", "aplicada", Arg.Any<IReadOnlyDictionary<string, object?>>());
    }
}
