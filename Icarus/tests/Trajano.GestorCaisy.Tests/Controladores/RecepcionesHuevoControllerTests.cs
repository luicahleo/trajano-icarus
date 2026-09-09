using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Trajano.GestorCaisy.Controllers;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Controladores;

// SP9C: la bandeja de recepción de huevo lista con filtro y paginación, el
// detalle habilita la confirmación solo para despachos en estado Despachado,
// la confirmación exige su pantalla propia y el recibo solo se sirve cuando la
// API lo tiene (despacho Recibido).
public class RecepcionesHuevoControllerTests
{
    private readonly ApiIcarusFalsa _api = new();
    private readonly RecepcionesHuevoController _controlador;

    public RecepcionesHuevoControllerTests()
    {
        _controlador = new RecepcionesHuevoController(_api)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>()),
        };
        _controlador.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task IndexListaConLosFiltrosYLasNotificaciones()
    {
        _api.PaginaDeDespachosHuevo = new PaginaDespachosHuevoApi(
            [new DespachoHuevoResumenApi(
                Guid.NewGuid(), "Despachado", new(2025, 11, 2), 10, 2950, 1602.75m)],
            1);
        _api.NotificacionesDeDespachosHuevo = new BandejaNotificacionesDespachoHuevoApi(
            [new NotificacionDespachoHuevoApi(
                Guid.NewGuid(), "DespachoRecibido", Guid.NewGuid(),
                new(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc), false, null)],
            1);

        var vista = await _controlador.Index(new FiltrosDespachosHuevoVista(), default);

        var modelo = Assert.IsType<BandejaDespachosHuevoVista>(((ViewResult)vista).Model);
        Assert.Equal(1, modelo.Pagina.Total);
        Assert.Equal(1, modelo.Notificaciones.Contador);
        Assert.NotNull(_api.UltimosFiltrosDespachosHuevo);
        Assert.Equal(1, _api.UltimosFiltrosDespachosHuevo!.Pagina);
        Assert.Equal(1, _api.VecesListarNotificacionesHuevo);
    }

    [Fact]
    public async Task IndexPropagaLaPaginaPedida()
    {
        await _controlador.Index(new FiltrosDespachosHuevoVista { Pagina = 3, TamanoPagina = 50 }, default);

        Assert.Equal(3, _api.UltimosFiltrosDespachosHuevo!.Pagina);
        Assert.Equal(50, _api.UltimosFiltrosDespachosHuevo.TamanoPagina);
    }

    [Fact]
    public async Task DetallesHabilitaLaConfirmacionSoloParaDespachado()
    {
        var id = Guid.NewGuid();
        _api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Despachado");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDespachoHuevoDetalle>(((ViewResult)vista).Model);
        Assert.True(modelo.PuedeConfirmarse);
        Assert.Equal(2950, modelo.Despacho.TotalHuevos);
    }

    [Fact]
    public async Task DetallesDeUnRecibidoNoPermiteConfirmar()
    {
        var id = Guid.NewGuid();
        _api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaDespachoHuevoDetalle>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeConfirmarse);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        _api.ErrorDeObtenerDespachoHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Detalles(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task ConfirmarRecepcionPantallaDeUnDespachadoMuestraLaVista()
    {
        var id = Guid.NewGuid();
        _api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Despachado");

        var vista = await _controlador.ConfirmarRecepcionPantalla(id, default);

        var modelo = Assert.IsType<VistaDespachoHuevoDetalle>(((ViewResult)vista).Model);
        Assert.Equal(id, modelo.Despacho.Id);
        Assert.True(modelo.PuedeConfirmarse);
    }

    [Fact]
    public async Task ConfirmarRecepcionPantallaDeUnRecibidoRedirigeAlDetalle()
    {
        var id = Guid.NewGuid();
        _api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");

        var resultado = await _controlador.ConfirmarRecepcionPantalla(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(RecepcionesHuevoController.Detalles), redireccion.ActionName);
    }

    [Fact]
    public async Task ConfirmarRecepcionConfirmaYRedirigeConMensajeDeExito()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.ConfirmarRecepcion(id, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(RecepcionesHuevoController.Detalles), redireccion.ActionName);
        Assert.Equal(1, _api.VecesConfirmarRecepcion);
        Assert.Equal(id, _api.UltimaRecepcionConfirmada);
        Assert.Equal(
            "La recepción quedó confirmada; el emisor fue notificado.",
            _controlador.TempData["Exito"]);
    }

    [Fact]
    public async Task ConfirmarRecepcionConConflictoGuardaElErrorYRedirige()
    {
        var id = Guid.NewGuid();
        _api.ErrorDeConfirmarRecepcion = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.ConfirmarRecepcion(id, default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1, _api.VecesConfirmarRecepcion);
        Assert.Equal("Conflicto con el estado actual", _controlador.TempData["Error"]);
    }

    [Fact]
    public async Task ReciboDevuelveArchivoPdf()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Recibo(id, default);

        var archivo = Assert.IsType<FileStreamResult>(resultado);
        Assert.Equal("application/pdf", archivo.ContentType);
        Assert.Equal("recibo.pdf", archivo.FileDownloadName);
        Assert.Equal(id, _api.UltimoReciboHuevo);
    }

    [Fact]
    public async Task ReciboInexistenteDevuelve404()
    {
        _api.ErrorDeReciboHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Recibo(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task MarcarLeidaMarcaYVuelveALaBandeja()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.MarcarLeida(id, default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1, _api.VecesMarcarLeidaHuevo);
        Assert.Equal(id, _api.UltimaNotificacionHuevoMarcada);
    }
}
