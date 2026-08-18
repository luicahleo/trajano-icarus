using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class DesactivarGalponHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly DesactivarGalponHandler _handler;
    public DesactivarGalponHandlerTests() => _handler = new DesactivarGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new DesactivarGalponCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivaYGuarda()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        await _handler.Handle(new DesactivarGalponCommand(galpon.Id), CancellationToken.None);
        Assert.False(galpon.EstaActivo);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
