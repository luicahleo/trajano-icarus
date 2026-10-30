using FluentValidation;
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
    private readonly IRepositorioTareasVacunacion _tareas = Substitute.For<IRepositorioTareasVacunacion>();
    private readonly IImportadorCronogramaVacunacion _importador = Substitute.For<IImportadorCronogramaVacunacion>();
    private readonly IRegistroVuelo _vuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidad = Substitute.For<IUnidadTrabajoGestionAvicola>();

    private static ProgramaVacunacion Borrador()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", Hoy.AddDays(-10), 1000, null);
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(10, "B", null, null),
            new DatosItemPlanVacunacion(3, "A", "Agua de bebida", null),
        ]);
        return programa;
    }

    private static ProgramaVacunacion Publicado()
    {
        var programa = Borrador();
        programa.Publicar();
        return programa;
    }

    private void ParaGestion(ProgramaVacunacion programa) =>
        _programas.ObtenerParaGestionAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);

    private void SinProgramaParaGestion() =>
        _programas.ObtenerParaGestionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);

    [Fact]
    public async Task CrearConDatosValidosGuardaUnBorradorYNarra()
    {
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var id = await handler.Handle(new("PLAN CAISY 1000", 1000, null), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _programas.Received(1).Agregar(Arg.Is<ProgramaVacunacion>(p =>
            p.Nombre == "PLAN CAISY 1000" && p.CantidadAves == 1000 && p.EstaActivo
            && p.Estado == EstadoProgramaVacunacion.Borrador && p.FechaEmision == null));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir(
            Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.vacunacion.programas.crear"),
            "alta", "aplicada", Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    [Fact]
    public async Task CrearConNombreDuplicadoLanzaConflict()
    {
        _programas.ExisteNombreAsync("PLAN CAISY 1000", null, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CrearProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new("PLAN CAISY 1000", 1000, null), CancellationToken.None));

        Assert.Equal("No se pudo registrar el programa de vacunación.", ex.Message);
        _programas.DidNotReceive().Agregar(Arg.Any<ProgramaVacunacion>());
    }

    [Fact]
    public async Task ActualizarInexistenteLanzaNotFound()
    {
        SinProgramaParaGestion();
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), "X", 100, null), CancellationToken.None));

        Assert.Equal("Programa de vacunación no encontrado.", ex.Message);
    }

    [Fact]
    public async Task ActualizarConNombreDeOtroProgramaLanzaConflict()
    {
        var programa = Borrador();
        ParaGestion(programa);
        _programas.ExisteNombreAsync("OTRO PLAN", programa.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(programa.Id, "OTRO PLAN", 100, null), CancellationToken.None));

        Assert.Equal("PLAN CAISY 1000", programa.Nombre);
    }

    [Fact]
    public async Task ActualizarUnBorradorGuarda()
    {
        var programa = Borrador();
        ParaGestion(programa);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id, "PLAN RENOMBRADO", 2000, "nota"), CancellationToken.None);

        Assert.Equal("PLAN RENOMBRADO", programa.Nombre);
        Assert.Equal(2000, programa.CantidadAves);
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarUnPublicadoLanzaReglaNegocioSinGuardar()
    {
        var programa = Publicado();
        ParaGestion(programa);
        var handler = new ActualizarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new(programa.Id, "PLAN RENOMBRADO", 2000, null), CancellationToken.None));

        Assert.Equal("PLAN CAISY 1000", programa.Nombre);
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConProgramaInexistenteLanzaNotFound()
    {
        SinProgramaParaGestion();
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid(), new MemoryStream()), CancellationToken.None));
    }

    [Fact]
    public async Task ImportarSobreUnPublicadoLanzaReglaNegocioSinLeerElExcel()
    {
        var programa = Publicado();
        ParaGestion(programa);
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None));

        _importador.DidNotReceive().Importar(Arg.Any<Stream>());
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConErroresLanzaValidationYSinGuardarNada()
    {
        var programa = Borrador();
        ParaGestion(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [],
            [new ErrorFilaImportacion(3, "La edad debe ser un número entero mayor que cero.")]));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None));

        Assert.Equal("Fila 3: La edad debe ser un número entero mayor que cero.", ex.Errors.Single().ErrorMessage);
        Assert.Equal("Cronograma", ex.Errors.Single().PropertyName);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarValidoReemplazaElCronogramaYDevuelveLaCantidad()
    {
        var programa = Borrador();
        ParaGestion(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [new ItemCronogramaImportado(1, "NEWCASTLE", "Gota ocular", null),
             new ItemCronogramaImportado(3, "BIO COCCIVET R", "Agua de bebida", "Ayuno 2 horas")],
            []));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        var importados = await handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None);

        Assert.Equal(2, importados);
        Assert.Equal(4, programa.Items.Count);
        Assert.Equal(2, programa.Items.Count(i => i.EstaActivo));
        _vuelo.Received().Decidir(
            Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.vacunacion.programas.importar-cronograma"),
            "importacion", "aplicada", Arg.Any<IReadOnlyDictionary<string, object?>>());
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportarConFechaEnElExcelEstableceLaFechaDeEmision()
    {
        var programa = new ProgramaVacunacion("PLAN CAISY 1000", null, 1000, null);
        ParaGestion(programa);
        _importador.Importar(Arg.Any<Stream>()).Returns(new ResultadoImportacionCronograma(
            [new ItemCronogramaImportado(1, "NEWCASTLE", "Gota ocular", null)],
            [], new DateOnly(2026, 8, 6)));
        var handler = new ImportarCronogramaExcelHandler(_programas, _importador, _vuelo, _unidad);

        await handler.Handle(new(programa.Id, new MemoryStream()), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 8, 6), programa.FechaEmision);
    }

    [Fact]
    public async Task PublicarInexistenteLanzaNotFound()
    {
        SinProgramaParaGestion();
        var handler = new PublicarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task PublicarUnBorradorConCronogramaLoPublicaYNarraLosItems()
    {
        var programa = Borrador();
        ParaGestion(programa);
        var handler = new PublicarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.Equal(EstadoProgramaVacunacion.Publicado, programa.Estado);
        _vuelo.Received().Decidir(
            Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.vacunacion.programas.publicar"),
            "publicacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => (int)c["ItemsPublicados"]! == 2));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublicarSinCronogramaLanzaReglaNegocioSinGuardar()
    {
        var programa = new ProgramaVacunacion("PLAN VACIO", null, 1000, null);
        ParaGestion(programa);
        var handler = new PublicarProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new(programa.Id), CancellationToken.None));

        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EliminarUnBorradorLoDejaInactivoYGuarda()
    {
        var programa = Borrador();
        ParaGestion(programa);
        var handler = new EliminarBorradorProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.False(programa.EstaActivo);
        _vuelo.Received().Decidir(
            Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.vacunacion.programas.eliminar-borrador"),
            "eliminacion", "aplicada", Arg.Any<IReadOnlyDictionary<string, object?>?>());
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EliminarUnPublicadoLanzaReglaNegocioSinGuardar()
    {
        var programa = Publicado();
        ParaGestion(programa);
        var handler = new EliminarBorradorProgramaVacunacionHandler(_programas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new(programa.Id), CancellationToken.None));

        Assert.True(programa.EstaActivo);
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarInexistenteLanzaNotFound()
    {
        SinProgramaParaGestion();
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _tareas, _vuelo, _unidad);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivarUnPublicadoDesactivaLasPendientesDelProgramaYGuarda()
    {
        var programa = Publicado();
        ParaGestion(programa);
        _tareas.DesactivarPendientesDeProgramaAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(3);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _tareas, _vuelo, _unidad);

        await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.False(programa.EstaActivo);
        await _tareas.Received(1).DesactivarPendientesDeProgramaAsync(programa.Id, Arg.Any<CancellationToken>());
        _vuelo.Received().Decidir(
            Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.vacunacion.programas.desactivar"),
            "desactivacion", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(c => (int)c["TareasPendientesDesactivadas"]! == 3));
        await _unidad.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarUnBorradorLanzaReglaNegocioSinTocarTareas()
    {
        var programa = Borrador();
        ParaGestion(programa);
        var handler = new DesactivarProgramaVacunacionHandler(_programas, _tareas, _vuelo, _unidad);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new(programa.Id), CancellationToken.None));

        await _tareas.DidNotReceive().DesactivarPendientesDeProgramaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unidad.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarParaElTenantConsultaSoloLosPublicados()
    {
        _programas.ListarPublicadosAsync(Arg.Any<CancellationToken>()).Returns([Publicado()]);
        var handler = new ListarProgramasVacunacionHandler(_programas);

        var lista = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal("PLAN CAISY 1000", Assert.Single(lista).Nombre);
        await _programas.DidNotReceive().ListarParaGestionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerParaElTenantUnNoPublicadoLanzaNotFound()
    {
        _programas.ObtenerPublicadoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProgramaVacunacion?)null);
        var handler = new ObtenerProgramaVacunacionHandler(_programas);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerParaElTenantDevuelveItemsActivosOrdenadosPorEdad()
    {
        var programa = Publicado();
        _programas.ObtenerPublicadoPorIdAsync(programa.Id, Arg.Any<CancellationToken>()).Returns(programa);
        var handler = new ObtenerProgramaVacunacionHandler(_programas);

        var detalle = await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.Equal(2, detalle.Items.Count);
        Assert.Equal(3, detalle.Items[0].EdadDia);
        Assert.Equal(10, detalle.Items[1].EdadDia);
        Assert.Equal("A", detalle.Items[0].Vacuna);
    }

    [Fact]
    public async Task ListarParaGestionMapeaEstadoActividadYCantidadDeItems()
    {
        var borrador = Borrador();
        var desactivado = Publicado();
        desactivado.Desactivar();
        _programas.ListarParaGestionAsync(Arg.Any<CancellationToken>()).Returns([borrador, desactivado]);
        var handler = new ListarProgramasVacunacionGestionHandler(_programas);

        var lista = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(2, lista.Count);
        Assert.Equal("Borrador", lista[0].Estado);
        Assert.True(lista[0].EstaActivo);
        Assert.Equal(2, lista[0].CantidadItems);
        Assert.Equal("Publicado", lista[1].Estado);
        Assert.False(lista[1].EstaActivo);
    }

    [Fact]
    public async Task ObtenerParaGestionInexistenteLanzaNotFound()
    {
        SinProgramaParaGestion();
        var handler = new ObtenerProgramaVacunacionGestionHandler(_programas);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerParaGestionDevuelveEstadoEItemsActivos()
    {
        var programa = Borrador();
        ParaGestion(programa);
        var handler = new ObtenerProgramaVacunacionGestionHandler(_programas);

        var detalle = await handler.Handle(new(programa.Id), CancellationToken.None);

        Assert.Equal("Borrador", detalle.Estado);
        Assert.True(detalle.EstaActivo);
        Assert.Equal(new[] { 3, 10 }, detalle.Items.Select(i => i.EdadDia));
    }
}
