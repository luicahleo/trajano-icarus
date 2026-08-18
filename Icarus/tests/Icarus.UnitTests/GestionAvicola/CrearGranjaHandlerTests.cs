using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;

namespace Icarus.UnitTests.GestionAvicola;

public class CrearGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly CrearGranjaHandler _handler;

    public CrearGranjaHandlerTests()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _handler = new CrearGranjaHandler(_granjas, _usuarioActual, _unidadTrabajo);
    }

    [Fact]
    public async Task SinClienteEnElClaimLanzaUnauthorized()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new CrearGranjaCommand("Granja Norte"), CancellationToken.None));
        _granjas.DidNotReceive().Agregar(Arg.Any<Granja>());
    }

    [Fact]
    public async Task GranjaActivaExistenteLanzaConflictGenerico()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>()).Returns(new Granja(ClienteId, "Granja Vieja"));
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new CrearGranjaCommand("Granja Norte"), CancellationToken.None));
        Assert.Equal("No se pudo registrar la granja.", ex.Message);
    }

    [Fact]
    public async Task NombreDuplicadoLanzaConflictGenerico()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>()).Returns((Granja?)null);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Norte", Arg.Any<CancellationToken>()).Returns(true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new CrearGranjaCommand("  Granja Norte "), CancellationToken.None));
        Assert.Equal("No se pudo registrar la granja.", ex.Message);
    }

    [Fact]
    public async Task DatosValidosCreanYGuardan()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>()).Returns((Granja?)null);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Norte", Arg.Any<CancellationToken>()).Returns(false);
        var id = await _handler.Handle(new CrearGranjaCommand(" Granja Norte "), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, id);
        _granjas.Received(1).Agregar(Arg.Is<Granja>(g => g.ClienteId == ClienteId && g.Nombre == "Granja Norte" && g.EstaActivo));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
