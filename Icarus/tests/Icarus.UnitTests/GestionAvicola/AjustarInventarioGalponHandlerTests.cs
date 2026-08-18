using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class AjustarInventarioGalponHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly AjustarInventarioGalponHandler _handler;
    public AjustarInventarioGalponHandlerTests() => _handler = new AjustarInventarioGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new AjustarInventarioGalponCommand(Guid.NewGuid(), 100), CancellationToken.None));
    }

    [Fact]
    public async Task AjusteValidoGuarda()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        await _handler.Handle(new AjustarInventarioGalponCommand(galpon.Id, 4500), CancellationToken.None);
        Assert.Equal(4500, galpon.GallinasActuales);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
