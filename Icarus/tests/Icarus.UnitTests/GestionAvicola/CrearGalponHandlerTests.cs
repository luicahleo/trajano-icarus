using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class CrearGalponHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly DateOnly Ayer = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly Granja _granja = new(ClienteId, "Granja Norte");
    private readonly CrearGalponHandler _handler;

    public CrearGalponHandlerTests() => _handler = new CrearGalponHandler(_granjas, _galpones, _unidadTrabajo);
    private CrearGalponCommand ComandoValido() => new(_granja.Id, "1", 5000, 4800, Ayer, "Norte");

    [Fact]
    public async Task GranjaInexistenteOAjenaLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Granja?)null);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(ComandoValido(), CancellationToken.None));
        Assert.Equal("Granja no encontrado.", ex.Message);
    }

    [Fact]
    public async Task GranjaInactivaNoPermiteAlta()
    {
        var inactiva = new Granja(ClienteId, "Vieja"); inactiva.Desactivar();
        _granjas.ObtenerPorIdAsync(_granja.Id, Arg.Any<CancellationToken>()).Returns(inactiva);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(ComandoValido(), CancellationToken.None));
    }

    [Fact]
    public async Task NumeroDuplicadoLanzaConflictGenerico()
    {
        _granjas.ObtenerPorIdAsync(_granja.Id, Arg.Any<CancellationToken>()).Returns(_granja);
        _galpones.ExisteNumeroAsync(_granja.Id, "1", Arg.Any<CancellationToken>()).Returns(true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(ComandoValido(), CancellationToken.None));
        Assert.Equal("No se pudo registrar el galpón.", ex.Message);
    }

    [Fact]
    public async Task DatosValidosCreanConElTenantDeLaGranjaYGuardan()
    {
        _granjas.ObtenerPorIdAsync(_granja.Id, Arg.Any<CancellationToken>()).Returns(_granja);
        _galpones.ExisteNumeroAsync(_granja.Id, "1", Arg.Any<CancellationToken>()).Returns(false);
        var id = await _handler.Handle(ComandoValido(), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, id);
        _galpones.Received(1).Agregar(Arg.Is<Galpon>(g => g.GranjaId == _granja.Id && g.ClienteId == ClienteId && g.GallinasActuales == 4800));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
