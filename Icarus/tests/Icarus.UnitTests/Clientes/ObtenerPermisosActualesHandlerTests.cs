using Icarus.Clientes.Application.Autorizacion;
using NSubstitute;

namespace Icarus.UnitTests.Clientes;

public class ObtenerPermisosActualesHandlerTests
{
    private readonly IConsultaPermisosActuales _consulta = Substitute.For<IConsultaPermisosActuales>();
    private readonly ObtenerPermisosActualesHandler _handler;

    public ObtenerPermisosActualesHandlerTests() => _handler = new ObtenerPermisosActualesHandler(_consulta);

    [Fact]
    public async Task DelegaEnLaConsultaConLosIdsDelUsuarioActual()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var esperado = new PermisosActuales([], ["Granjas"]);
        _consulta.ObtenerAsync(clienteId, trabajadorId, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _handler.Handle(
            new ObtenerPermisosActualesQuery(clienteId, trabajadorId), CancellationToken.None);

        Assert.Same(esperado, resultado);
    }

    [Fact]
    public async Task SinTrabajadorConsultaComoCliente()
    {
        var clienteId = Guid.NewGuid();
        var esperado = new PermisosActuales(["GestionAvicola"], ["Granjas", "Galpones"]);
        _consulta.ObtenerAsync(clienteId, null, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _handler.Handle(
            new ObtenerPermisosActualesQuery(clienteId, null), CancellationToken.None);

        Assert.Same(esperado, resultado);
    }
}
