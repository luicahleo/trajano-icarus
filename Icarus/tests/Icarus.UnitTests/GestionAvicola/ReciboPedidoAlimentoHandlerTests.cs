using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboPedidoAlimentoHandlerTests
{
    [Fact]
    public async Task DevuelveNotFoundSiElPedidoNoExiste()
    {
        var repositorio = Substitute.For<IRepositorioPedidosAlimento>();
        repositorio.ObtenerConHistorialAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);
        var renderer = Substitute.For<IReciboPedidoRenderer>();
        var handler = new ObtenerReciboPedidoPdfHandler(repositorio, renderer);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerReciboPedidoPdfQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
