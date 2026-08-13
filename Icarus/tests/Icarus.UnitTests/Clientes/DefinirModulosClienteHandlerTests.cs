using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class DefinirModulosClienteHandlerTests
{
    private readonly IRepositorioClientes _clientes = Substitute.For<IRepositorioClientes>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DefinirModulosClienteHandler _handler;

    public DefinirModulosClienteHandlerTests() => _handler = new DefinirModulosClienteHandler(_clientes, _unitOfWork);

    [Fact]
    public async Task ModulosValidosSeAsignanAlCliente()
    {
        var cliente = new Cliente("Granja", "20100000001");
        _clientes.ObtenerGestionablePorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        await _handler.Handle(
            new DefinirModulosClienteCommand(cliente.Id, ["GestionAvicola", "controlacceso"]),
            CancellationToken.None);

        Assert.True(cliente.TieneModulo(Modulos.GestionAvicola));
        Assert.True(cliente.TieneModulo(Modulos.ControlAcceso));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListaVaciaQuitaTodosLosModulos()
    {
        var cliente = new Cliente("Granja", "20100000001");
        cliente.DefinirModulos(Modulos.GestionAvicola);
        _clientes.ObtenerGestionablePorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        await _handler.Handle(new DefinirModulosClienteCommand(cliente.Id, []), CancellationToken.None);

        Assert.Equal(Modulos.Ninguno, cliente.ModulosHabilitados);
    }

    [Fact]
    public async Task ClienteInexistenteLanzaNotFound()
    {
        _clientes.ObtenerGestionablePorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new DefinirModulosClienteCommand(Guid.NewGuid(), ["GestionAvicola"]),
                CancellationToken.None));
    }
}
