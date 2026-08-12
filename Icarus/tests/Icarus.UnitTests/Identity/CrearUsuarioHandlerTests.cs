using Icarus.BuildingBlocks.Domain;
using Icarus.Identity.Application.Usuarios;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Identity;

public class CrearUsuarioHandlerTests
{
    private readonly IRegistradorUsuarios _registrador = Substitute.For<IRegistradorUsuarios>();
    private readonly CrearUsuarioHandler _handler;

    public CrearUsuarioHandlerTests() => _handler = new CrearUsuarioHandler(_registrador);

    [Fact]
    public async Task RegistroExitosoDevuelveIdYNormalizaElRol()
    {
        var id = Guid.NewGuid();
        _registrador.RegistrarAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(id);

        var resultado = await _handler.Handle(
            new CrearUsuarioCommand("nueva@icarus.test", "Contrasena-123", "cliente", Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.Equal(id, resultado);
        await _registrador.Received(1).RegistrarAsync(
            "nueva@icarus.test", "Contrasena-123", "Cliente",
            Arg.Any<Guid?>(), Arg.Is<Guid?>(t => t == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailDuplicadoLanzaConflictGenerico()
    {
        _registrador.RegistrarAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(
            new CrearUsuarioCommand("nueva@icarus.test", "Contrasena-123", "SoporteTecnico", null, null),
            CancellationToken.None));
    }
}
