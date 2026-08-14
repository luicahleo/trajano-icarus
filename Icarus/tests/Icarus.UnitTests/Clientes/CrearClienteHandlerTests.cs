using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class CrearClienteHandlerTests
{
    private readonly IRepositorioClientes _clientes = Substitute.For<IRepositorioClientes>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CrearClienteHandler _handler;

    public CrearClienteHandlerTests() => _handler = new CrearClienteHandler(_clientes, _unitOfWork);

    [Fact]
    public async Task IdentificadorFiscalNuevoCreaYGuarda()
    {
        _clientes.ExisteIdentificadorFiscalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var id = await _handler.Handle(
            new CrearClienteCommand("Granja", "20100000001", "granja@icarus.test", "Contrasena-Prueba-1"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _clientes.Received(1).Agregar(Arg.Any<Cliente>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdentificadorFiscalDuplicadoLanzaConflictGenerico()
    {
        _clientes.ExisteIdentificadorFiscalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(
                new CrearClienteCommand("Granja", "20100000001", "granja@icarus.test", "Contrasena-Prueba-1"),
                CancellationToken.None));

        Assert.Equal("No se pudo registrar el cliente.", ex.Message);
        _clientes.DidNotReceive().Agregar(Arg.Any<Cliente>());
    }
}
