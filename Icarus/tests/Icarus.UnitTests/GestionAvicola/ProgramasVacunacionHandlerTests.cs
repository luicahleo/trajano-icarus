using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ProgramasVacunacionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioProgramasVacunacion _programas = Substitute.For<IRepositorioProgramasVacunacion>();
    private readonly IImportadorCronogramaVacunacion _importador = Substitute.For<IImportadorCronogramaVacunacion>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static ProgramaVacunacion ProgramaDemo()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", Hoy.AddDays(-10), 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(10, "B", null, null),
            new DatosItemPlanVacunacion(3, "A", "Agua de bebida", null),
        ]);
        return programa;
    }

    [Fact]
    public async Task CrearConDatosValidosGuardaYNarra()
    {
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var id = await handler.Handle(new("PLAN CAISY 1000", Hoy, 1000, null), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _programas.Received(1).Agregar(Arg.Is<ProgramaVacunacion>(p =>
            p.Nombre == "PLAN CAISY 1000" && p.CantidadAves == 1000 && p.EstaActivo));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir("avicola.vacunacion.programas.crear", "alta", "aplicada",
            Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    [Fact]
    public async Task CrearConNombreDuplicadoLanzaConflict()
    {
        _programas.ExisteNombreAsync("PLAN CAISY 1000", null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new("PLAN CAISY 1000", Hoy, 1000, null), CancellationToken.None));

        Assert.Equal("No se pudo registrar el programa de vacunación.", ex.Message);
        _programas.DidNotReceive().Agregar(Arg.Any<ProgramaVacunacion>());
    }

    [Fact]
    public async Task ActualizarInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), "X", Hoy, 100, null), CancellationToken.None));

        Assert.Equal("Programa de vacunación no encontrado.", ex.Message);
    }

    [Fact]
    public async Task ActualizarConNombreDeOtroProgramaLanzaConflict()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _programas.ExisteNombreAsync("OTRO PLAN", programa.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(programa.Id, "OTRO PLAN", Hoy, 100, null), CancellationToken.None));

        Assert.Equal("PLAN CAISY 1000", programa.Nombre);
    }

    [Fact]
    public async Task ActualizarConDatosValidosGuarda()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _programas.ExisteNombreAsync("PLAN RENOMBRADO", programa.Id, Arg.Any<CancellationToken>()).Returns(false);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id, "PLAN RENOMBRADO", Hoy, 2000, "nota"), CancellationToken.None);

        Assert.Equal("PLAN RENOMBRADO", programa.Nombre);
        Assert.Equal(2000, programa.CantidadAves);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConProgramaInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), new MemoryStream()), CancellationToken.None));
    }

    [Fact]
    public async Task ImportarConErroresLanzaValidationYSinGuardarNada()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [],
            [new ErrorFilaImportacion(3, "La edad debe ser un número entero mayor que cero.")]));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None));

        Assert.Equal("Fila 3: La edad debe ser un número entero mayor que cero.", ex.Errors.Single().ErrorMessage);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarValidoReemplazaElCronogramaYDevuelveLaCantidad()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [new ItemCronogramaImportado(1, "NEWCASTLE", "Gota ocular", null),
             new ItemCronogramaImportado(3, "BIO COCCIVET R", "Agua de bebida", "Ayuno 2 horas")],
            []));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var importados = await handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None);

        Assert.Equal(2, importados);
        Assert.Equal(4, programa.Items.Count);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        _vuelo.Received().Decidir("avicola.vacunacion.programas.importar-cronograma", "importacion", "aplicada",
            Arg.Any<IReadOnlyDictionary<string, object?>>());
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivarMarcaInactivoYGuarda()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _unidad);

        await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.False(programa.EstaActivo);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarSinRolAdministradorIgnoraElIncluirInactivos()
    {
        _usuario.Rol.Returns("Cliente");
        var handler = new ListarProgramasVacunacionHandler(_programas, _usuario);

        await handler.Handle(new(true), CancellationToken.None);

        await _programas.Received(1).ListarAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarComoAdministradorSiIncluyeInactivos()
    {
        _usuario.Rol.Returns("Administrador");
        _programas.ListarAsync(true, Arg.Any<CancellationToken>()).Returns([ProgramaDemo()]);
        var handler = new ListarProgramasVacunacionHandler(_programas, _usuario);

        var lista = await handler.Handle(new(true), CancellationToken.None);

        Assert.Single(lista);
        Assert.Equal("PLAN CAISY 1000", lista[0].Nombre);
    }

    [Fact]
    public async Task ObtenerInexistenteLanzaNotFound()
    {
        _programas.ObtenerPorIdIncluyendoInactivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerInactivoSinSerAdministradorLanzaNotFound()
    {
        var programa = ProgramaDemo();
        programa.Desactivar();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(programa.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerDevuelveDetalleConItemsActivosOrdenadosPorEdad()
    {
        var programa = ProgramaDemo();
        _programas.ObtenerPorIdIncluyendoInactivosAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        _usuario.Rol.Returns("Cliente");
        var handler = new ObtenerProgramaVacunacionHandler(_programas, _usuario);

        var detalle = await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.Equal(2, detalle.Items.Count);
        Assert.Equal(3, detalle.Items[0].EdadDia);
        Assert.Equal(10, detalle.Items[1].EdadDia);
        Assert.Equal("A", detalle.Items[0].Vacuna);
    }
}
