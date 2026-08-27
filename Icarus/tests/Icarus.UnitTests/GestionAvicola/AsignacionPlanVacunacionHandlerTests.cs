using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class AsignacionPlanVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioProgramasVacunacion _programas = Substitute.For<IRepositorioProgramasVacunacion>();
    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static Galpon GalponDemo() => new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Hoy.AddDays(-30), null);

    private static ProgramaVacunacion ProgramaDemo()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", Hoy.AddDays(-60), 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
            new DatosItemPlanVacunacion(10, "NEWCASTLE", "Gota ocular", "Ayuno 2 horas"),
        ]);
        return programa;
    }

    private AsignarPlanVacunacionHandler HandlerAsignar() =>
        new(_galpones, _programas, _tareas, _vuelo, _unidad);

    [Fact]
    public async Task AsignarConGalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerAsignar().Handle(new(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
        _tareas.DidNotReceive().Agregar(Arg.Any<TareaVacunacion>());
    }

    [Fact]
    public async Task AsignarConProgramaInexistenteOInactivoLanzaNotFound()
    {
        var galpon = GalponDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerAsignar().Handle(new(galpon.Id, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Programa de vacunación no encontrado.", ex.Message);
    }

    [Fact]
    public async Task AsignarProgramaSinCronogramaLanzaConflict()
    {
        var galpon = GalponDemo();
        var programa = new ProgramaVacunacion("PLAN VACIO", Hoy.AddDays(-60), 1000, null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None));

        Assert.Equal("No se pudo asignar el plan de vacunación.", ex.Message);
        _tareas.DidNotReceive().Agregar(Arg.Any<TareaVacunacion>());
    }

    [Fact]
    public async Task AsignarCreaUnaTareaPorItemConFechaDesdeElNacimientoDelLote()
    {
        var galpon = GalponDemo();
        var programa = ProgramaDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

        await HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None);

        var nacimiento = Hoy.AddDays(-30);
        _tareas.Received(2).Agregar(Arg.Any<TareaVacunacion>());
        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.GalponId == galpon.Id && t.ClienteId == galpon.ClienteId
            && t.ProgramaVacunacionId == programa.Id
            && t.EdadDia == 3 && t.Vacuna == "BIO COCCIVET R" && t.ModoAplicacion == "Agua de bebida"
            && t.FechaProgramada == nacimiento.AddDays(3)
            && t.Estado == EstadoTareaVacunacion.Pendiente && t.EstaActivo));
        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.EdadDia == 10 && t.ObservacionesProgramadas == "Ayuno 2 horas"
            && t.FechaProgramada == nacimiento.AddDays(10)));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarUsaLaFechaDelExcelCuandoElItemLaTrae()
    {
        var galpon = GalponDemo();
        var programa = new ProgramaVacunacion("PLAN CON FECHAS", null, 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null, new DateOnly(2026, 8, 8)),
            new DatosItemPlanVacunacion(10, "NEWCASTLE", null, null),
        ]);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

        await HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None);

        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.EdadDia == 3 && t.FechaProgramada == new DateOnly(2026, 8, 8)));
        _tareas.Received(1).Agregar(Arg.Is<TareaVacunacion>(t =>
            t.EdadDia == 10 && t.FechaProgramada == galpon.FechaNacimientoLote.AddDays(10)));
    }

    [Fact]
    public async Task AsignarDesactivaLasPendientesAnterioresYNarraElResultado()
    {
        var galpon = GalponDemo();
        var programa = ProgramaDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _programas.ObtenerPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _tareas.DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(2);

        await HandlerAsignar().Handle(new(galpon.Id, programa.Id), CancellationToken.None);

        await _tareas.Received(1).DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir("avicola.vacunacion.asignar", "asignacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c =>
                Equals(c["TareasCreadas"], 2) && Equals(c["TareasPendientesDesactivadas"], 2)));
    }

    [Fact]
    public async Task QuitarConGalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Galpon?)null);
        var handler = new QuitarPlanVacunacionHandler(_galpones, _tareas, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task QuitarDesactivaLasPendientesYGuarda()
    {
        var galpon = GalponDemo();
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        _tareas.DesactivarPendientesDeGalponAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(1);
        var handler = new QuitarPlanVacunacionHandler(_galpones, _tareas, _vuelo, _unidad);

        await handler.Handle(new(galpon.Id), CancellationToken.None);

        _vuelo.Received().Decidir("avicola.vacunacion.quitar-plan", "quitar", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => Equals(c["TareasPendientesDesactivadas"], 1)));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
