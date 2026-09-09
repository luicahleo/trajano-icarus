using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Controllers;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Controladores;

public class PreciosHuevoControllerTests
{
    private readonly ApiIcarusFalsa _api = new();
    private readonly PreciosHuevoController _controlador;

    public PreciosHuevoControllerTests()
    {
        _controlador = new PreciosHuevoController(_api)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>()),
        };
        _controlador.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = UsuarioGestorcaisy() },
        };
    }

    [Fact]
    public async Task IndexDevuelveLaListaDeResumenes()
    {
        _api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 6, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsAssignableFrom<IReadOnlyList<PublicacionPrecioHuevoResumenApi>>(
            ((ViewResult)vista).Model);
        Assert.Equal(_api.ResumenesHuevo.Count, modelo.Count);
    }

    [Fact]
    public async Task DetallesDeUnBorradorPermiteEditarYDescartarPeroNoAnular()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesHuevo>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Publicacion.Id);
        Assert.True(modelo.PuedeEditarse);
        Assert.True(modelo.PuedeDescartarse);
        Assert.False(modelo.PuedeAnularse);
        Assert.False(modelo.EsPublicacionEfectiva);
    }

    [Fact]
    public async Task DetallesDeUnaPublicacionFuturaPermiteAnular()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(
            id, "Publicada", fechaVigencia: "2999-01-01");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetallesHuevo>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeEditarse);
        Assert.False(modelo.PuedeDescartarse);
        Assert.True(modelo.PuedeAnularse);
        Assert.False(modelo.EsPublicacionEfectiva);
    }

    [Fact]
    public async Task DetallesDeUnaPublicacionEfectivaNoPermiteEditarNiAnularNiDescartar()
    {
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(
            Guid.NewGuid(), "Publicada", fechaVigencia: "2025-01-01");

        var vista = await _controlador.Detalles(Guid.NewGuid(), default);

        var modelo = Assert.IsType<VistaDetallesHuevo>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeEditarse);
        Assert.False(modelo.PuedeDescartarse);
        Assert.False(modelo.PuedeAnularse);
        Assert.True(modelo.EsPublicacionEfectiva);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        _api.ErrorDeObtenerHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Detalles(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task ConfirmarPublicacionMuestraElResumen()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var vista = await _controlador.ConfirmarPublicacion(id, default);

        var modelo = Assert.IsType<VistaDetallesHuevo>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Publicacion.Id);
    }

    [Fact]
    public async Task PublicarInvocaAlClienteYRedirigeConExito()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Publicar(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
        Assert.Equal(id, _api.UltimoPublicadoHuevo);
        Assert.Contains("publicada", _controlador.TempData["Exito"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicarConDiscrepanciaRegresaAConfirmarConElError()
    {
        _api.ErrorDePublicarHuevo = new ErrorApiException(400, "Solicitud inválida", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Documento"] = ["El «Precio actual» del documento no coincide con la publicación vigente."],
            });

        var resultado = await _controlador.Publicar(Guid.NewGuid(), default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.ConfirmarPublicacion), redireccion.ActionName);
        Assert.Contains("Precio actual", _controlador.TempData["Error"]?.ToString());
        Assert.Equal(1, _api.VecesPublicarHuevo);
    }

    [Fact]
    public async Task AnularInvocaAlClienteYRedirigeConExito()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Anular(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
        Assert.Equal(id, _api.UltimoAnuladoHuevo);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task AnularDeUnaEfectivaMuestraElMensajeDeLaApi()
    {
        _api.ErrorDeAnularHuevo = new ErrorApiException(400, "Error de negocio", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                [""] = ["Una publicación ya efectiva no se puede anular."],
            });

        var resultado = await _controlador.Anular(Guid.NewGuid(), default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains("ya efectiva", _controlador.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task ConfirmarDescartarMuestraElResumenDelBorrador()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var vista = await _controlador.ConfirmarDescartar(id, default);

        var modelo = Assert.IsType<VistaDetallesHuevo>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Publicacion.Id);
        Assert.True(modelo.PuedeDescartarse);
    }

    [Fact]
    public async Task ConfirmarDescartarDeUnaNoBorradorRedirigeADetalles()
    {
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(Guid.NewGuid(), "Publicada");

        var resultado = await _controlador.ConfirmarDescartar(Guid.NewGuid(), default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task DescartarInvocaAlClienteYRedirigeAlHistorial()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Descartar(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Index), redireccion.ActionName);
        Assert.Equal(id, _api.UltimoDescartadoHuevo);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task DescartarConErrorDeNegocioRegresaAConfirmarConElMensaje()
    {
        var id = Guid.NewGuid();
        _api.ErrorDeDescartarHuevo = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Descartar(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.ConfirmarDescartar), redireccion.ActionName);
        Assert.Contains("Conflicto", _controlador.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task DescartarUnBorradorYaDescartadoEsIdempotenteYRedirigeAlHistorial()
    {
        var id = Guid.NewGuid();
        _api.ErrorDeDescartarHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Descartar(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Index), redireccion.ActionName);
        Assert.Equal(1, _api.VecesDescartarHuevo);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task ImportarSinArchivoAgregaErrorDeModelo()
    {
        var resultado = await _controlador.Importar(archivo: null, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.True(_controlador.ModelState.ContainsKey("archivo"));
        Assert.Equal(0, _api.VecesImportarHuevo);
    }

    [Fact]
    public async Task ImportarConExcelRedirigeAlBorradorCreado()
    {
        var bytes = "PK\x03\x04 planilla de precios de prueba"u8.ToArray();
        var archivo = CrearArchivo(bytes, "precios-huevo.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var resultado = await _controlador.Importar(archivo, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
        Assert.Equal(_api.IdDeImportacionHuevo, redireccion.RouteValues!["id"]);
        Assert.Equal(bytes, _api.UltimoExcelImportado);
    }

    [Fact]
    public async Task ImportarDemasiadoGrandeMuestraElMensaje()
    {
        _api.ErrorDeImportarHuevo = new ErrorApiException(413, null);
        var archivo = CrearArchivo("PK\x03\x04"u8.ToArray(), "precios-huevo.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var resultado = await _controlador.Importar(archivo, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("tamaño", _controlador.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task EditarMuestraElFormularioDelBorrador()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var vista = await _controlador.Editar(id, default);

        var formulario = Assert.IsType<FormularioBorradorHuevoVista>(((ViewResult)vista).Model);
        Assert.Equal(id, formulario.PublicacionId);
        Assert.Equal(new DateOnly(2025, 11, 2), formulario.FechaNotificacion);
        Assert.Equal(0.50m, formulario.Servicio);
        Assert.Equal(2, formulario.Detalles.Count);
        Assert.Equal("Primera", formulario.Detalles[0].Tamano);
        Assert.Equal(0.045m, formulario.Detalles[0].PrecioAlProductor);
    }

    [Fact]
    public async Task EditarDeUnaNoBorradorRedirigeADetalles()
    {
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(Guid.NewGuid(), "Publicada");

        var resultado = await _controlador.Editar(Guid.NewGuid(), default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task GuardarBorradorReconstruyeElComando()
    {
        var id = Guid.NewGuid();
        var formulario = new FormularioBorradorHuevoVista
        {
            PublicacionId = id,
            FechaNotificacion = new(2025, 11, 2),
            FechaVigencia = new(2025, 12, 1),
            Servicio = 0.55m,
            Detalles =
            [
                new FilaDetalleHuevoVista
                {
                    Tamano = "Primera", PrecioAlProductor = 0.046m, PrecioActualDocumento = 0.045m,
                },
            ],
        };

        var resultado = await _controlador.Editar(id, formulario, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosHuevoController.Detalles), redireccion.ActionName);
        var comando = _api.UltimoComandoHuevo!;
        Assert.Equal(id, comando.PublicacionId);
        Assert.Equal(new(2025, 11, 2), comando.FechaNotificacion);
        Assert.Equal(0.55m, comando.Servicio);
        var detalle = Assert.Single(comando.Detalles);
        Assert.Equal("Primera", detalle.Tamano);
        Assert.Equal(0.046m, detalle.PrecioAlProductor);
    }

    [Fact]
    public async Task GuardarBorradorConConflictoMuestraElMensaje()
    {
        _api.ErrorDeActualizarHuevo = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Editar(
            Guid.NewGuid(), new FormularioBorradorHuevoVista(), default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("modificó",
            _controlador.ModelState[string.Empty]!.Errors[0].ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardarBorradorConErroresDeValidacionLosMuestra()
    {
        _api.ErrorDeActualizarHuevo = new ErrorApiException(400, "Solicitud inválida", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Detalles[0].PrecioAlProductor"] = ["El precio debe ser mayor que cero."],
            });

        var resultado = await _controlador.Editar(
            Guid.NewGuid(), new FormularioBorradorHuevoVista(), default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("mayor que cero",
            _controlador.ModelState["Detalles[0].PrecioAlProductor"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task DescargarDevuelveElExcelComoAdjunto()
    {
        var id = Guid.NewGuid();
        _api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Publicada");

        var resultado = await _controlador.DocumentoOriginal(id, default);

        var archivo = Assert.IsType<FileStreamResult>(resultado);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            archivo.ContentType);
        Assert.Contains("precios-huevo-2025-11-02", archivo.FileDownloadName);
        Assert.Equal(1, _api.VecesDescargarHuevo);
    }

    [Fact]
    public async Task DescargarSinDocumentoDevuelve404()
    {
        _api.ErrorDeDescargarHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.DocumentoOriginal(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    private static IFormFile CrearArchivo(byte[] bytes, string nombre, string tipo)
    {
        var memoria = new MemoryStream(bytes);
        return new FormFile(memoria, 0, memoria.Length, "archivo", nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = tipo,
        };
    }

    private static ClaimsPrincipal UsuarioGestorcaisy()
    {
        var identidad = new ClaimsIdentity("prueba");
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimRol, ConstantesAutorizacion.RolGestorCaisy));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimFuncionalidadesCaisy, "2"));
        return new ClaimsPrincipal(identidad);
    }
}
