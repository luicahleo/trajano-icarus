using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class ObtenerGranjaHandlerTests
{
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly ObtenerGranjaHandler _handler;

    public ObtenerGranjaHandlerTests() => _handler = new ObtenerGranjaHandler(_granjas);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Granja?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new ObtenerGranjaQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GranjaExistenteDevuelveResumen()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        var resumen = await _handler.Handle(new ObtenerGranjaQuery(granja.Id), CancellationToken.None);
        Assert.Equal(granja.Id, resumen.Id);
        Assert.Equal("Granja Norte", resumen.Nombre);
    }
}
