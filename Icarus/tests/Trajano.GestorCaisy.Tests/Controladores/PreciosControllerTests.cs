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

public class PreciosControllerTests
{
    private readonly ApiIcarusFalsa _api = new();
    private readonly PreciosController _controlador;

    public PreciosControllerTests()
    {
        _controlador = new PreciosController(_api)
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
        _api.Resumenes.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 12, true));

        var vista = await _controlador.Index(default);

        var modelo = Assert.IsAssignableFrom<IReadOnlyList<NotificacionPreciosResumenApi>>(
            ((ViewResult)vista).Model);
        Assert.Equal(_api.Resumenes.Count, modelo.Count);
    }

    [Fact]
    public async Task DetallesDeUnBorradorPermiteEditarPeroNoAnular()
    {
        var id = Guid.NewGuid();
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetalles>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Notificacion.Id);
        Assert.True(modelo.PuedeEditarse);
        Assert.False(modelo.PuedeAnularse);
    }

    [Fact]
    public async Task DetallesDeUnaPublicacionFuturaPermiteAnular()
    {
        var id = Guid.NewGuid();
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(
            id, "Publicada", vigenteDesde: "2999-01-01");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDetalles>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeEditarse);
        Assert.True(modelo.PuedeAnularse);
    }

    [Fact]
    public async Task DetallesDeUnaPublicacionEfectivaNoPermiteAnular()
    {
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(
            Guid.NewGuid(), "Publicada", vigenteDesde: "2025-01-01");

        var vista = await _controlador.Detalles(Guid.NewGuid(), default);

        var modelo = Assert.IsType<VistaDetalles>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeAnularse);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        _api.ErrorDeObtener = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Detalles(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task ConfirmarPublicacionMuestraElResumen()
    {
        var id = Guid.NewGuid();
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var vista = await _controlador.ConfirmarPublicacion(id, default);

        var modelo = Assert.IsType<VistaDetalles>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Notificacion.Id);
    }

    [Fact]
    public async Task PublicarInvocaAlClienteYRedirigeConExito()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Publicar(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.Detalles), redireccion.ActionName);
        Assert.Equal(id, _api.UltimoPublicado);
        Assert.Contains("publicada", _controlador.TempData["Exito"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicarConDiscrepanciaRegresaAConfirmarConElError()
    {
        _api.ErrorDePublicar = new ErrorApiException(400, "Solicitud inválida", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Documento"] = ["El «Precio actual» del documento no coincide con la publicación vigente."],
            });

        var resultado = await _controlador.Publicar(Guid.NewGuid(), default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.ConfirmarPublicacion), redireccion.ActionName);
        Assert.Contains("Precio actual", _controlador.TempData["Error"]?.ToString());
        Assert.Equal(1, _api.VecesPublicar);
    }

    [Fact]
    public async Task PublicarEnConflictoMuestraElMensajeDeLaApi()
    {
        _api.ErrorDePublicar = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Publicar(Guid.NewGuid(), default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains("Conflicto", _controlador.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task AnularInvocaAlClienteYRedirigeConExito()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Anular(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.Detalles), redireccion.ActionName);
        Assert.Equal(id, _api.UltimoAnulado);
        Assert.NotNull(_controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task AnularDeUnaEfectivaMuestraElMensajeDeLaApi()
    {
        _api.ErrorDeAnular = new ErrorApiException(400, "Error de negocio", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                [""] = ["Una publicación ya efectiva no se puede anular."],
            });

        var resultado = await _controlador.Anular(Guid.NewGuid(), default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains("ya efectiva", _controlador.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task ImportarSinArchivoAgregaErrorDeModelo()
    {
        var resultado = await _controlador.Importar(archivo: null, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.True(_controlador.ModelState.ContainsKey("archivo"));
        Assert.Equal(0, _api.VecesImportar);
    }

    [Fact]
    public async Task ImportarConArchivoRedirigeAlBorradorCreado()
    {
        var bytes = "%PDF-1.7 notificacion de prueba"u8.ToArray();
        var archivo = CrearArchivo(bytes);

        var resultado = await _controlador.Importar(archivo, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.Detalles), redireccion.ActionName);
        Assert.Equal(_api.IdDeImportacion, redireccion.RouteValues!["id"]);
        Assert.Equal(bytes, _api.UltimoPdfImportado);
    }

    [Fact]
    public async Task ImportarDemasiadoGrandeMuestraElMensaje()
    {
        _api.ErrorDeImportar = new ErrorApiException(413, null);
        var archivo = CrearArchivo("%PDF-1.7"u8.ToArray());

        var resultado = await _controlador.Importar(archivo, default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("tamaño", _controlador.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task EditarMuestraElFormularioDelBorrador()
    {
        var id = Guid.NewGuid();
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var vista = await _controlador.Editar(id, default);

        var formulario = Assert.IsType<FormularioBorradorVista>(((ViewResult)vista).Model);
        Assert.Equal(id, formulario.NotificacionId);
        Assert.Equal(new DateOnly(2025, 11, 2), formulario.FechaDocumento);
        Assert.Equal(2, formulario.Detalles.Count);
        Assert.Equal("Preiniciador", formulario.Detalles[0].TipoAlimento);
        Assert.Equal(118.50m, formulario.Detalles[0].PrecioFinalPor40Kg);
    }

    [Fact]
    public async Task EditarDeUnaNoBorradorRedirigeADetalles()
    {
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(Guid.NewGuid(), "Publicada");

        var resultado = await _controlador.Editar(Guid.NewGuid(), default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task GuardarBorradorReconstruyeElComando()
    {
        var id = Guid.NewGuid();
        var formulario = new FormularioBorradorVista
        {
            NotificacionId = id,
            FechaDocumento = new(2025, 11, 2),
            VigenteDesde = new(2025, 12, 1),
            AporteCaisy = 1.20m,
            Fondo = 0.60m,
            Servicios = 0.75m,
            Detalles =
            [
                new FilaDetalleVista
                {
                    TipoAlimento = "Crecimiento", Presentacion = "Granel",
                    PrecioFinalPor40Kg = 109.90m, PrecioActualDocumento = 108.00m,
                    EdadDesdeDias = null, EdadHastaDias = null,
                },
            ],
        };

        var resultado = await _controlador.Editar(id, formulario, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PreciosController.Detalles), redireccion.ActionName);
        var comando = _api.UltimoComando!;
        Assert.Equal(id, comando.NotificacionId);
        Assert.Equal(new(2025, 11, 2), comando.FechaDocumento);
        Assert.Equal(1.20m, comando.AporteCaisy);
        var detalle = Assert.Single(comando.Detalles);
        Assert.Equal("Crecimiento", detalle.TipoAlimento);
        Assert.Equal("Granel", detalle.Presentacion);
        Assert.Equal(109.90m, detalle.PrecioFinalPor40Kg);
    }

    [Fact]
    public async Task GuardarBorradorConConflictoMuestraElMensaje()
    {
        _api.ErrorDeActualizar = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Editar(Guid.NewGuid(), new FormularioBorradorVista(), default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("modificó",
            _controlador.ModelState[string.Empty]!.Errors[0].ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardarBorradorConErroresDeValidacionLosMuestra()
    {
        _api.ErrorDeActualizar = new ErrorApiException(400, "Solicitud inválida", erroresValidacion:
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Detalles[0].PrecioFinalPor40Kg"] = ["El precio debe ser mayor que cero."],
            });

        var resultado = await _controlador.Editar(Guid.NewGuid(), new FormularioBorradorVista(), default);

        Assert.IsType<ViewResult>(resultado);
        Assert.False(_controlador.ModelState.IsValid);
        Assert.Contains("mayor que cero",
            _controlador.ModelState["Detalles[0].PrecioFinalPor40Kg"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task DescargarDevuelveElPdfComoAdjunto()
    {
        var id = Guid.NewGuid();
        _api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Publicada");

        var resultado = await _controlador.DocumentoOriginal(id, default);

        var archivo = Assert.IsType<FileStreamResult>(resultado);
        Assert.Equal("application/pdf", archivo.ContentType);
        Assert.Contains("notificacion-precios-2025-11-02", archivo.FileDownloadName);
        Assert.Equal(1, _api.VecesDescargar);
    }

    [Fact]
    public async Task DescargarSinDocumentoDevuelve404()
    {
        _api.ErrorDeDescargar = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.DocumentoOriginal(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    private static IFormFile CrearArchivo(byte[] bytes)
    {
        var memoria = new MemoryStream(bytes);
        return new FormFile(memoria, 0, memoria.Length, "archivo", "notificacion.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }

    private static ClaimsPrincipal UsuarioGestorcaisy()
    {
        var identidad = new ClaimsIdentity("prueba");
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimRol, ConstantesAutorizacion.RolGestorCaisy));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimFuncionalidadesCaisy, "1"));
        return new ClaimsPrincipal(identidad);
    }
}
