using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class RenombrarGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly RenombrarGranjaHandler _handler;

    public RenombrarGranjaHandlerTests() => _handler = new RenombrarGranjaHandler(_granjas, _unidadTrabajo);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Granja?)null);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new RenombrarGranjaCommand(Guid.NewGuid(), "Nuevo"), CancellationToken.None));
        Assert.Equal("Granja no encontrado.", ex.Message);
    }

    [Fact]
    public async Task MismoNombreNoConsultaUnicidad()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        await _handler.Handle(new RenombrarGranjaCommand(granja.Id, " Granja Norte "), CancellationToken.None);
        await _granjas.DidNotReceive().ExisteNombreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NombreDuplicadoLanzaConflictGenerico()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Sur", Arg.Any<CancellationToken>()).Returns(true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(new RenombrarGranjaCommand(granja.Id, "Granja Sur"), CancellationToken.None));
        Assert.Equal("No se pudo renombrar la granja.", ex.Message);
        Assert.Equal("Granja Norte", granja.Nombre);
    }

    [Fact]
    public async Task NombreNuevoRenombraYGuarda()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Sur", Arg.Any<CancellationToken>()).Returns(false);
        await _handler.Handle(new RenombrarGranjaCommand(granja.Id, " Granja Sur "), CancellationToken.None);
        Assert.Equal("Granja Sur", granja.Nombre);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
