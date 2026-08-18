using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class ActualizarGalponHandlerTests
{
    private static readonly DateOnly Ayer = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly Galpon _galpon = new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Ayer, null);
    private readonly ActualizarGalponHandler _handler;
    public ActualizarGalponHandlerTests() => _handler = new ActualizarGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new ActualizarGalponCommand(Guid.NewGuid(), "2", null, 6000), CancellationToken.None));
        Assert.Equal("Galpon no encontrado.", ex.Message);
    }

    [Fact]
    public async Task NumeroDuplicadoLanzaConflictGenerico()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _galpones.ExisteNumeroAsync(_galpon.GranjaId, "2", Arg.Any<CancellationToken>()).Returns(true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(new ActualizarGalponCommand(_galpon.Id, "2", null, 6000), CancellationToken.None));
        Assert.Equal("No se pudo actualizar el galpón.", ex.Message);
    }

    [Fact]
    public async Task DatosValidosActualizanYGuardan()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _galpones.ExisteNumeroAsync(_galpon.GranjaId, "2", Arg.Any<CancellationToken>()).Returns(false);
        await _handler.Handle(new ActualizarGalponCommand(_galpon.Id, " 2 ", "Sur", 6000), CancellationToken.None);
        Assert.Equal("2", _galpon.Numero); Assert.Equal("Sur", _galpon.Descripcion); Assert.Equal(6000, _galpon.CapacidadMaxima);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
