using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;
public class RegistrarProduccionHandlerTests
{
    [Fact]
    public async Task IdempotenciaOcurreAntesDeAgregar()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        var key = Guid.NewGuid(); var existente = new RegistroProduccion(galpon.Id, galpon.ClienteId, DateOnly.FromDateTime(DateTime.UtcNow), default, 1, 0, 0, 0, 4800, key);
        var gs = Substitute.For<IRepositorioGalpones>(); var ps = Substitute.For<IRepositorioProduccion>(); var u = Substitute.For<IUnidadTrabajoGestionAvicola>();
        gs.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon); ps.ObtenerPorIdempotencyKeyAsync(galpon.Id, key, Arg.Any<CancellationToken>()).Returns(existente);
        var id = await new RegistrarProduccionHandler(gs, ps, u).Handle(new(galpon.Id, null, 2, 0, 0, 0, key), CancellationToken.None);
        Assert.Equal(existente.Id, id); ps.DidNotReceive().Agregar(Arg.Any<RegistroProduccion>()); await u.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
