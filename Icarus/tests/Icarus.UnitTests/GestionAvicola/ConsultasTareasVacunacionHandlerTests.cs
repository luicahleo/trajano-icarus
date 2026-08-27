using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ConsultasTareasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();

    private static TareaVacunacion Tarea(Guid galponId, Guid clienteId, DateOnly fechaProgramada, string vacuna) =>
        new(galponId, clienteId, Guid.NewGuid(), Guid.NewGuid(), 3, vacuna, null, null, fechaProgramada);

    [Fact]
    public async Task HistorialDeGalponAjenoLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        var handler = new ListarTareasPorGalponHandler(_galpones, _tareas);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
    }

    [Fact]
    public async Task HistorialDevuelveTodasLasTareasConSuEstado()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Hoy.AddDays(-30), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        var completada = Tarea(galpon.Id, galpon.ClienteId, Hoy.AddDays(-20), "A");
        completada.Completar(Hoy.AddDays(-20), null, Guid.NewGuid(), null);
        var pendiente = Tarea(galpon.Id, galpon.ClienteId, Hoy.AddDays(5), "B");
        _tareas.ListarPorGalponAsync(galpon.Id, Arg.Any<CancellationToken>())
            .Returns([pendiente, completada]);
        var handler = new ListarTareasPorGalponHandler(_galpones, _tareas);

        var historial = await handler.Handle(new(galpon.Id), CancellationToken.None);

        Assert.Equal(2, historial.Count);
        Assert.Equal("Completada", historial[0].Estado);
        Assert.Equal("Pendiente", historial[1].Estado);
    }

    [Fact]
    public async Task NotificacionSeparaVencidasYHoyDeLasProximas7Dias()
    {
        var clienteId = Guid.NewGuid();
        var galponId = Guid.NewGuid();
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        var vencida = Tarea(galponId, clienteId, Hoy.AddDays(-2), "VENCIDA");
        var deHoy = Tarea(galponId, clienteId, Hoy, "DE HOY");
        var proxima = Tarea(galponId, clienteId, Hoy.AddDays(5), "PROXIMA");
        _tareas.ListarNotificacionAsync(clienteId, Hoy, Hoy.AddDays(7), Arg.Any<CancellationToken>())
            .Returns([proxima, vencida, deHoy]);
        var handler = new ListarNotificacionVacunacionHandler(_tareas, _usuario);

        var notificacion = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(["VENCIDA", "DE HOY"], notificacion.VencidasYHoy.Select(t => t.Vacuna));
        Assert.Equal(["PROXIMA"], notificacion.Proximas.Select(t => t.Vacuna));
        await _tareas.Received(1).ListarNotificacionAsync(clienteId, Hoy, Hoy.AddDays(7), Arg.Any<CancellationToken>());
    }
}
