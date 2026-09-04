using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application.BalanceAlimentos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8C Tarea 5 (spec: "Balance"): solo los estados recibidos generan gasto, y
// siempre con la cantidad realmente recibida y el precio congelado al envío.
// El handler delega la consulta SQL canónica al repositorio; el rango y el
// tenant quedan acotados por la consulta.
public class BalanceAlimentosHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioBalanceAlimentos _repositorio =
        Substitute.For<IRepositorioBalanceAlimentos>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);

    private ObtenerBalanceAlimentosHandler CrearHandler() =>
        new(_repositorio, _usuarioActual, _registroVuelo);

    private static readonly DateOnly Desde = new(2026, 9, 1);
    private static readonly DateOnly Hasta = new(2026, 9, 30);

    [Fact]
    public async Task ElBalanceUsaElTenantDeLaSesionYDevuelveElTotalSumado()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _repositorio.ObtenerAsync(ClienteId, Desde, Hasta, Arg.Any<CancellationToken>())
            .Returns([
                new LineaBalanceAlimentos("PosturaUno", 100, 1, 18350m),
                new LineaBalanceAlimentos("PosturaDos", 50, 1, 9225m),
            ]);

        var balance = await CrearHandler().Handle(
            new ObtenerBalanceAlimentosQuery(Desde, Hasta), CancellationToken.None);

        Assert.Equal(2, balance.Lineas.Count);
        Assert.Equal(18350m + 9225m, balance.Total);
        Assert.Equal(Desde, balance.Desde);
        Assert.Equal(Hasta, balance.Hasta);
    }

    [Fact]
    public async Task ElBalanceRequiereUnaCuentaDeTenant()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(
                new ObtenerBalanceAlimentosQuery(Desde, Hasta), CancellationToken.None));

        await _repositorio.DidNotReceiveWithAnyArgs()
            .ObtenerAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), default);
    }

    [Fact]
    public async Task ElBalanceSinMovimientosDevuelveCero()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _repositorio.ObtenerAsync(ClienteId, Desde, Hasta, Arg.Any<CancellationToken>())
            .Returns([]);

        var balance = await CrearHandler().Handle(
            new ObtenerBalanceAlimentosQuery(Desde, Hasta), CancellationToken.None);

        Assert.Empty(balance.Lineas);
        Assert.Equal(0m, balance.Total);
    }

    [Fact]
    public async Task UnRangoInvertidoSeRechaza()
    {
        var resultado = await new ObtenerBalanceAlimentosValidator().ValidateAsync(
            new ObtenerBalanceAlimentosQuery(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1)),
            CancellationToken.None);

        Assert.False(resultado.IsValid);
    }
}
