using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboDespachoHuevoHandlerTests
{
    [Fact]
    public async Task ExigeQueElDespachoEsteRecibido()
    {
        var repositorio = Substitute.For<IRepositorioDespachosHuevo>();
        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        repositorio.ObtenerConHistorialAsync(despacho.Id, Arg.Any<CancellationToken>())
            .Returns(despacho);
        var handler = new ObtenerReciboDespachoHuevoPdfHandler(
            repositorio, Substitute.For<IReciboDespachoHuevoRenderer>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerReciboDespachoHuevoPdfQuery(despacho.Id), CancellationToken.None));
    }
}
