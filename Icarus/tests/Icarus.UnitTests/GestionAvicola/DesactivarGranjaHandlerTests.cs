using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class DesactivarGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRegistroVuelo _registroVuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly DesactivarGranjaHandler _handler;

    public DesactivarGranjaHandlerTests() => _handler = new DesactivarGranjaHandler(_granjas, _galpones, _registroVuelo, _unidadTrabajo);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Granja?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(new DesactivarGranjaCommand(Guid.NewGuid()), CancellationToken.None));
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivaGalponesActivosYNarraLaCascada()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        var galpones = new List<Galpon>
        {
            new(granja.Id, ClienteId, "1", 5000, 100, DateOnly.FromDateTime(DateTime.UtcNow), null),
            new(granja.Id, ClienteId, "2", 5000, 200, DateOnly.FromDateTime(DateTime.UtcNow), null),
        };
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _galpones.ListarActivosDeGranjaAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(galpones);
        await _handler.Handle(new DesactivarGranjaCommand(granja.Id), CancellationToken.None);
        Assert.False(granja.EstaActivo);
        Assert.All(galpones, g => Assert.False(g.EstaActivo));
        _registroVuelo.Received(1).Decidir("avicola.granjas.desactivar", "cascada_galpones", "aplicada", Arg.Is<IReadOnlyDictionary<string, object?>>(d => (int)d["GalponesDesactivados"]! == 2));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinGalponesNoNarraCascada()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _galpones.ListarActivosDeGranjaAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(new List<Galpon>());
        await _handler.Handle(new DesactivarGranjaCommand(granja.Id), CancellationToken.None);
        _registroVuelo.DidNotReceive().Decidir(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
