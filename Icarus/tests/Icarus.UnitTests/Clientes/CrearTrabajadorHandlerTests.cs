using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Clientes.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class CrearTrabajadorHandlerTests
{
    private readonly IRepositorioClientes _clientes = Substitute.For<IRepositorioClientes>();
    private readonly IRepositorioTrabajadores _trabajadores = Substitute.For<IRepositorioTrabajadores>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CrearTrabajadorHandler _handler;

    public CrearTrabajadorHandlerTests() =>
        _handler = new CrearTrabajadorHandler(_clientes, _trabajadores, _unitOfWork);

    private static CrearTrabajadorCommand ComandoValido(Guid clienteId) =>
        new(clienteId, "Nombre Ficticio", "00000000", "Operario", new DateOnly(2026, 1, 15));

    [Fact]
    public async Task TrabajadorValidoSeCreaYGuarda()
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(new Cliente(clienteId, "Granja", "20100000001"));
        _trabajadores.ExisteDocumentoAsync(clienteId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var id = await _handler.Handle(ComandoValido(clienteId), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _trabajadores.Received(1).Agregar(Arg.Any<Trabajador>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClienteInexistenteOAjenoLanzaNotFound()
    {
        // El filtro de tenant devuelve null tanto para un cliente inexistente
        // como para uno ajeno: el handler no distingue (anti-enumeración).
        _clientes.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(ComandoValido(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DocumentoDuplicadoLanzaConflictGenerico()
    {
        var clienteId = Guid.NewGuid();
        _clientes.ObtenerPorIdAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns(new Cliente(clienteId, "Granja", "20100000001"));
        _trabajadores.ExisteDocumentoAsync(clienteId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(ComandoValido(clienteId), CancellationToken.None));

        Assert.Equal("No se pudo registrar el trabajador.", ex.Message);
    }
}
