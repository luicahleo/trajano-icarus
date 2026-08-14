using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Host.Servicios;
using Icarus.Identity.Application.RegistroCuentas;
using MediatR;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Host;

public class AltaCuentasServicioTests
{
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly IRegistradorUsuarios _registrador = Substitute.For<IRegistradorUsuarios>();
    private readonly AltaCuentasServicio _servicio;

    public AltaCuentasServicioTests() => _servicio = new AltaCuentasServicio(_mediator, _registrador);

    [Fact]
    public async Task ClienteCreadoRegistraCuentaRolClienteYDevuelveId()
    {
        var clienteId = Guid.NewGuid();
        var cuentaId = Guid.NewGuid();
        var comando = new CrearClienteCommand("Granja", "20100000001", "cliente@icarus.test", "Contrasena-123");
        _mediator.Send(comando, Arg.Any<CancellationToken>()).Returns(clienteId);
        _registrador.RegistrarAsync(
                "cliente@icarus.test", "Contrasena-123", "Cliente", clienteId, null, Arg.Any<CancellationToken>())
            .Returns(cuentaId);

        var resultado = await _servicio.CrearClienteConCuentaAsync(comando, CancellationToken.None);

        Assert.Equal(clienteId, resultado);
        await _registrador.Received(1).RegistrarAsync(
            "cliente@icarus.test", "Contrasena-123", "Cliente", clienteId, null, Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Is<SuspenderClienteCommand>(c => c.ClienteId == clienteId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CuentaDeClienteFallidaSuspendeElClienteYDevuelveConflict()
    {
        var clienteId = Guid.NewGuid();
        var comando = new CrearClienteCommand("Granja", "20100000001", "cliente@icarus.test", "Contrasena-123");
        _mediator.Send(comando, Arg.Any<CancellationToken>()).Returns(clienteId);
        _registrador.RegistrarAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _servicio.CrearClienteConCuentaAsync(comando, CancellationToken.None));

        Assert.Equal("No se pudo registrar el cliente.", ex.Message);
        await _mediator.Received(1).Send(
            Arg.Is<SuspenderClienteCommand>(c => c.ClienteId == clienteId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrabajadorCreadoRegistraCuentaRolTrabajadorYDevuelveId()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var cuentaId = Guid.NewGuid();
        var comando = new CrearTrabajadorCommand(
            clienteId, "Nombre", "00000000", "Operario", new DateOnly(2026, 1, 15),
            "trabajador@icarus.test", "Contrasena-123");
        _mediator.Send(comando, Arg.Any<CancellationToken>()).Returns(trabajadorId);
        _registrador.RegistrarAsync(
                "trabajador@icarus.test", "Contrasena-123", "Trabajador", clienteId, trabajadorId,
                Arg.Any<CancellationToken>())
            .Returns(cuentaId);

        var resultado = await _servicio.CrearTrabajadorConCuentaAsync(comando, CancellationToken.None);

        Assert.Equal(trabajadorId, resultado);
        await _registrador.Received(1).RegistrarAsync(
            "trabajador@icarus.test", "Contrasena-123", "Trabajador", clienteId, trabajadorId,
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Is<DesactivarTrabajadorCommand>(c => c.TrabajadorId == trabajadorId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CuentaDeTrabajadorFallidaDesactivaElTrabajadorYDevuelveConflict()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var comando = new CrearTrabajadorCommand(
            clienteId, "Nombre", "00000000", "Operario", new DateOnly(2026, 1, 15),
            "trabajador@icarus.test", "Contrasena-123");
        _mediator.Send(comando, Arg.Any<CancellationToken>()).Returns(trabajadorId);
        _registrador.RegistrarAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _servicio.CrearTrabajadorConCuentaAsync(comando, CancellationToken.None));

        Assert.Equal("No se pudo registrar el trabajador.", ex.Message);
        await _mediator.Received(1).Send(
            Arg.Is<DesactivarTrabajadorCommand>(c => c.TrabajadorId == trabajadorId),
            Arg.Any<CancellationToken>());
    }
}
