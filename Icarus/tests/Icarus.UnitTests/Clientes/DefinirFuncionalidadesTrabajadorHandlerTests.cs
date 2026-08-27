using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Clientes.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class DefinirFuncionalidadesTrabajadorHandlerTests
{
    private readonly IRepositorioClientes _clientes = Substitute.For<IRepositorioClientes>();
    private readonly IRepositorioTrabajadores _trabajadores = Substitute.For<IRepositorioTrabajadores>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DefinirFuncionalidadesTrabajadorHandler _handler;

    public DefinirFuncionalidadesTrabajadorHandlerTests() =>
        _handler = new DefinirFuncionalidadesTrabajadorHandler(_clientes, _trabajadores, _unitOfWork);

    private static Cliente ClienteConModulo(Modulos modulos)
    {
        var cliente = new Cliente("Granja", "20100000001");
        cliente.DefinirModulos(modulos);
        return cliente;
    }

    private static Trabajador TrabajadorValido(Guid clienteId) =>
        new(clienteId, "Nombre Ficticio", "00000000", "Operario", new DateOnly(2026, 1, 15));

    [Fact]
    public async Task FuncionalidadesDeModuloHabilitadoSeAsignan()
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(ClienteConModulo(Modulos.GestionAvicola));
        var trabajador = TrabajadorValido(clienteId);
        _trabajadores.ObtenerPorIdAsync(trabajador.Id, Arg.Any<CancellationToken>()).Returns(trabajador);

        await _handler.Handle(
            new DefinirFuncionalidadesTrabajadorCommand(clienteId, trabajador.Id, ["ProduccionHuevos", "mortalidad", "Vacunacion"]),
            CancellationToken.None);

        Assert.True(trabajador.Funcionalidades.HasFlag(Funcionalidades.ProduccionHuevos));
        Assert.True(trabajador.Funcionalidades.HasFlag(Funcionalidades.Mortalidad));
        Assert.True(trabajador.Funcionalidades.HasFlag(Funcionalidades.Vacunacion));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FuncionalidadDeModuloNoHabilitadoLanzaReglaDeNegocio()
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(ClienteConModulo(Modulos.Ninguno));
        var trabajador = TrabajadorValido(clienteId);
        _trabajadores.ObtenerPorIdAsync(trabajador.Id, Arg.Any<CancellationToken>()).Returns(trabajador);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            _handler.Handle(
                new DefinirFuncionalidadesTrabajadorCommand(clienteId, trabajador.Id, ["Granjas"]),
                CancellationToken.None));

        Assert.Equal("Funcionalidad no disponible para este cliente.", ex.Message);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Granjas")]
    [InlineData("Galpones")]
    [InlineData("Alimentacion")]
    [InlineData("Despachos")]
    [InlineData("Precios")]
    public async Task FuncionalidadNoAsignableLanzaMensajeGenerico(string funcionalidad)
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(ClienteConModulo(Modulos.GestionAvicola));
        var trabajador = TrabajadorValido(clienteId);
        _trabajadores.ObtenerPorIdAsync(trabajador.Id, Arg.Any<CancellationToken>()).Returns(trabajador);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            _handler.Handle(new DefinirFuncionalidadesTrabajadorCommand(clienteId, trabajador.Id, [funcionalidad]), CancellationToken.None));

        Assert.Equal("Funcionalidad no disponible para este cliente.", ex.Message);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClienteInexistenteLanzaNotFound()
    {
        _clientes.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(
                new DefinirFuncionalidadesTrabajadorCommand(Guid.NewGuid(), Guid.NewGuid(), ["Granjas"]),
                CancellationToken.None));
    }

    [Fact]
    public async Task TrabajadorInexistenteLanzaNotFound()
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(ClienteConModulo(Modulos.GestionAvicola));
        _trabajadores.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Trabajador?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(
                new DefinirFuncionalidadesTrabajadorCommand(clienteId, Guid.NewGuid(), ["Granjas"]),
                CancellationToken.None));
    }
}
