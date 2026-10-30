using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Trajano.GestorCaisy.Controllers;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Controladores;

public class ProgramasVacunacionControllerTests
{
    private readonly ApiIcarusFalsa _api = new();
    private readonly ProgramasVacunacionController _controlador;

    public ProgramasVacunacionControllerTests()
    {
        _controlador = new ProgramasVacunacionController(_api)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>()),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static IFormFile Archivo() =>
        new FormFile(new MemoryStream("PK planilla"u8.ToArray()), 0, 11, "archivo", "plan.xlsx");

    [Fact]
    public async Task IndexOrdenaBorradoresPublicadosEInactivosYEtiquetaElEstado()
    {
        _api.ResumenesVacunacion.Add(new(Guid.NewGuid(), "B INACTIVO", null, 1000, "Publicado", false, 3));
        _api.ResumenesVacunacion.Add(new(Guid.NewGuid(), "C PUBLICADO", null, 1000, "Publicado", true, 3));
        _api.ResumenesVacunacion.Add(new(Guid.NewGuid(), "A BORRADOR", null, 1000, "Borrador", true, 0));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsType<VistaProgramasVacunacion>(((ViewResult)vista).Model);
        Assert.Equal(new[] { "A BORRADOR", "C PUBLICADO", "B INACTIVO" }, modelo.Programas.Select(p => p.Programa.Nombre));
        Assert.Equal(new[] { "Borrador", "Publicado", "Inactivo" }, modelo.Programas.Select(p => p.Etiqueta));
        Assert.Equal("chip--inactivo", modelo.Programas[2].ClaseChip);
    }

    [Fact]
    public async Task DetallesDeUnBorradorConCronogramaPermiteEditarYPublicar()
    {
        var id = Guid.NewGuid();
        _api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Borrador");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesProgramaVacunacion>(((ViewResult)vista).Model);
        Assert.True(modelo.PuedeEditarse);
        Assert.True(modelo.PuedePublicarse);
        Assert.False(modelo.PuedeDesactivarse);
    }

    [Fact]
    public async Task DetallesDeUnBorradorSinCronogramaNoPermitePublicar()
    {
        var id = Guid.NewGuid();
        _api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Borrador", cantidadItems: 0);

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesProgramaVacunacion>(((ViewResult)vista).Model);
        Assert.True(modelo.PuedeEditarse);
        Assert.False(modelo.PuedePublicarse);
    }

    [Fact]
    public async Task DetallesDeUnPublicadoSoloPermiteDesactivar()
    {
        var id = Guid.NewGuid();
        _api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Publicado");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesProgramaVacunacion>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeEditarse);
        Assert.False(modelo.PuedePublicarse);
        Assert.True(modelo.PuedeDesactivarse);
        Assert.Equal("Publicado", modelo.Etiqueta);
    }

    [Fact]
    public async Task DetallesDeUnInactivoNoPermiteNingunaAccion()
    {
        var id = Guid.NewGuid();
        _api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Publicado", estaActivo: false);

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesProgramaVacunacion>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeEditarse || modelo.PuedePublicarse || modelo.PuedeDesactivarse);
        Assert.Equal("Inactivo", modelo.Etiqueta);
    }

    [Fact]
    public async Task NuevoValidoCreaElBorradorYRedirigeAlDetalle()
    {
        var resultado = await _controlador.Nuevo(
            new FormularioProgramaVacunacionVista { Nombre = "PLAN 1000", CantidadAves = 1000 }, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(ProgramasVacunacionController.Detalles), redireccion.ActionName);
        Assert.Equal(_api.IdDeProgramaCreado, redireccion.RouteValues!["id"]);
        Assert.Equal(new DatosProgramaVacunacionApi("PLAN 1000", 1000, null), _api.UltimosDatosPrograma);
    }

    [Fact]
    public async Task NuevoConNombreEnUsoMuestraElErrorEnElCampo()
    {
        _api.ErrorDeVacunacion = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Nuevo(
            new FormularioProgramaVacunacionVista { Nombre = "PLAN 1000", CantidadAves = 1000 }, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(_controlador.ModelState[nameof(FormularioProgramaVacunacionVista.Nombre)]!.Errors,
            e => e.ErrorMessage.Contains("Ya existe un programa con ese nombre", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EditarUnPublicadoRedirigeAlDetalle()
    {
        var id = Guid.NewGuid();
        _api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Publicado");

        var resultado = await _controlador.Editar(id, default);

        Assert.Equal(nameof(ProgramasVacunacionController.Detalles),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);
    }

    [Fact]
    public async Task CronogramaSinArchivoPideElExcel()
    {
        var resultado = await _controlador.Cronograma(Guid.NewGuid(), null, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.Equal(0, _api.VecesImportarCronograma);
        Assert.False(_controlador.ModelState.IsValid);
    }

    [Fact]
    public async Task CronogramaConFilasInvalidasMuestraCadaErrorPorFila()
    {
        _api.ErrorDeVacunacion = new ErrorApiException(400, "Solicitud inválida",
            erroresValidacion: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Cronograma"] =
                [
                    "Fila 2: La edad debe ser un número entero mayor que cero.",
                    "Fila 5: La vacuna es obligatoria.",
                ],
            });

        var resultado = await _controlador.Cronograma(Guid.NewGuid(), Archivo(), default);

        Assert.IsType<ViewResult>(resultado);
        var errores = _controlador.ModelState["Cronograma"]!.Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Contains("Fila 2: La edad debe ser un número entero mayor que cero.", errores);
        Assert.Contains("Fila 5: La vacuna es obligatoria.", errores);
    }

    [Fact]
    public async Task CronogramaValidoInformaLaCantidadYRedirigeAlDetalle()
    {
        var id = Guid.NewGuid();
        _api.ItemsImportadosVacunacion = 9;

        var resultado = await _controlador.Cronograma(id, Archivo(), default);

        Assert.Equal(nameof(ProgramasVacunacionController.Detalles),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.Equal("Se importaron 9 ítems del cronograma.", _controlador.TempData["Exito"]);
        Assert.NotNull(_api.UltimoCronogramaImportado);
    }

    [Fact]
    public async Task EliminarRepetidoSeTrataComoExitoYaAplicado()
    {
        _api.ErrorDeVacunacion = new ErrorApiException(404);

        var resultado = await _controlador.Eliminar(Guid.NewGuid(), default);

        Assert.Equal(nameof(ProgramasVacunacionController.Index),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task PublicarRechazadoAvisaYVuelveAlDetalle()
    {
        _api.ErrorDeVacunacion = new ErrorApiException(400, "Error de negocio");

        var resultado = await _controlador.Publicar(Guid.NewGuid(), default);

        Assert.Equal(nameof(ProgramasVacunacionController.Detalles),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.NotNull(_controlador.TempData["Error"]);
    }

    [Fact]
    public async Task DesactivarAplicaYVuelveAlDetalle()
    {
        var resultado = await _controlador.Desactivar(Guid.NewGuid(), default);

        Assert.Equal(nameof(ProgramasVacunacionController.Detalles),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.Equal(1, _api.VecesDesactivarPrograma);
    }
}
