using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// Desglose visible del crédito (spec, ítem 2 del backlog): el handler
// combina el saldo (sin tocar) con los ajustes nuevos, y exige rol Cliente
// — la misma regla que EnviarPedidoAlimentoHandler exige para confirmar un
// envío con crédito insuficiente (ver PedidosAlimentoHandlerTests.cs).
public class ObtenerBalanceCreditoHuevoHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioBalanceCreditoHuevo _repositorio =
        Substitute.For<IRepositorioBalanceCreditoHuevo>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();

    public ObtenerBalanceCreditoHuevoHandlerTests()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _usuarioActual.Rol.Returns("Cliente");
    }

    private ObtenerBalanceCreditoHuevoHandler CrearHandler() => new(_repositorio, _usuarioActual);

    [Fact]
    public async Task CombinaSaldoYAjustesDelRepositorio()
    {
        _repositorio.ObtenerSaldoDisponibleAsync(ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(1250m);
        var ajustes = new List<AjusteCreditoHuevoResumen>
        {
            new(Guid.NewGuid(), 45m, "Corrección de precio Extra.", new DateOnly(2026, 9, 1)),
        };
        _repositorio.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>()).Returns(ajustes);

        var resultado = await CrearHandler().Handle(
            new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None);

        Assert.Equal(1250m, resultado.SaldoDisponible);
        Assert.Same(ajustes, resultado.Ajustes);
    }

    [Fact]
    public async Task SinAjustesDevuelveListaVacia()
    {
        _repositorio.ObtenerSaldoDisponibleAsync(ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(0m);
        _repositorio.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>())
            .Returns(new List<AjusteCreditoHuevoResumen>());

        var resultado = await CrearHandler().Handle(
            new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None);

        Assert.Empty(resultado.Ajustes);
    }

    [Fact]
    public async Task SinCuentaDeTenantFalla()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task UnTrabajadorNoPuedeConsultarElCredito()
    {
        _usuarioActual.Rol.Returns("Trabajador");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearHandler().Handle(new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None));

        await _repositorio.DidNotReceive().ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _repositorio.DidNotReceive().ObtenerAjustesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
