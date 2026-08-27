using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EjecucionTareasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static TareaVacunacion TareaPendiente() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            3, "BIO COCCIVET R", null, null, Hoy);

    private CompletarTareaVacunacionHandler HandlerCompletar() => new(_tareas, _usuario, _vuelo, _unidad);

    [Fact]
    public async Task CompletarInexistenteLanzaNotFound()
    {
        _tareas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TareaVacunacion?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerCompletar().Handle(new(Guid.NewGuid(), null, null, null), CancellationToken.None));

        Assert.Equal("Tarea de vacunación no encontrada.", ex.Message);
    }

    [Fact]
    public async Task CompletarUsaHoyPorDefectoYRegistraElUsuarioActual()
    {
        var tarea = TareaPendiente();
        var usuarioId = Guid.NewGuid();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(usuarioId);

        await HandlerCompletar().Handle(new(tarea.Id, null, null, null), CancellationToken.None);

        Assert.Equal(EstadoTareaVacunacion.Completada, tarea.Estado);
        Assert.Equal(Hoy, tarea.FechaAplicacion);
        Assert.Equal(usuarioId, tarea.CompletadaPor);
        Assert.Null(tarea.AvesVacunadas);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletarConFechaPasadaYDetalleLosConserva()
    {
        var tarea = TareaPendiente();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(Guid.NewGuid());

        await HandlerCompletar().Handle(new(tarea.Id, Hoy.AddDays(-2), 950, "parcial"), CancellationToken.None);

        Assert.Equal(Hoy.AddDays(-2), tarea.FechaAplicacion);
        Assert.Equal(950, tarea.AvesVacunadas);
        _vuelo.Received().Decidir("avicola.vacunacion.completar", "aplicacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => Equals(c["AvesVacunadas"], 950)));
    }

    [Fact]
    public async Task CompletarTareaYaCerradaLanzaConflict()
    {
        var tarea = TareaPendiente();
        tarea.Completar(Hoy, null, Guid.NewGuid(), null);
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        _usuario.UsuarioId.Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            HandlerCompletar().Handle(new(tarea.Id, null, null, null), CancellationToken.None));

        Assert.Equal("No se pudo completar la tarea de vacunación.", ex.Message);
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarInexistenteLanzaNotFound()
    {
        _tareas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TareaVacunacion?)null);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task CancelarPendienteRegistraMotivoYGuarda()
    {
        var tarea = TareaPendiente();
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        await handler.Handle(new(tarea.Id, "Lote diezmado"), CancellationToken.None);

        Assert.Equal(EstadoTareaVacunacion.Cancelada, tarea.Estado);
        Assert.Equal("Lote diezmado", tarea.MotivoCancelacion);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarTareaYaCerradaLanzaConflict()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar(null);
        _tareas.ObtenerPorIdAsync(tarea.Id, Arg.Any<CancellationToken>()).Returns(tarea);
        var handler = new CancelarTareaVacunacionHandler(_tareas, _unidad);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(tarea.Id, null), CancellationToken.None));

        Assert.Equal("No se pudo cancelar la tarea de vacunación.", ex.Message);
    }
}
