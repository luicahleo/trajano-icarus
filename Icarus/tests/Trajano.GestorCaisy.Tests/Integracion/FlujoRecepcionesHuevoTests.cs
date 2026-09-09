using System.Net;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

// Flujo HTML completo de la bandeja de recepción de huevo (SP9C) contra la API
// falsa: listado con filtro y paginación, detalle por tamaño, confirmación de
// recepción con protección antiforgery, recibo solo tras confirmar y
// notificaciones con marcado de lectura.
public class FlujoRecepcionesHuevoTests
{
    [Fact]
    public async Task SinLaFuncionalidadLaRutaDeRecepcionesQuedaProhibida()
    {
        using var aplicacion = new AplicacionDePruebas();
        // Solo GestorPedidoAlimento (bit 1): no alcanza para la recepción de huevo.
        var cliente = await aplicacion.AccederAsync(funcCaisy: 1);

        var respuesta = await cliente.GetAsync("/RecepcionesHuevo");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Denegado", respuesta.Headers.Location!.AbsolutePath);
        var denegado = await cliente.GetStringAsync("/Sesion/Denegado");
        Assert.Contains("No tiene acceso", denegado);
    }

    [Fact]
    public async Task BandejaVaciaMuestraElEstadoVacio()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);

        var html = await cliente.GetStringAsync("/RecepcionesHuevo");

        Assert.Contains("No hay despachos de huevo con ese filtro.", html);
        Assert.Contains("Novedades para CAISY", html);
    }

    [Fact]
    public async Task BandejaMuestraFilasFiltrosYPaginacion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        aplicacion.Api.PaginaDeDespachosHuevo = new PaginaDespachosHuevoApi(
            Enumerable.Range(0, 3).Select(_ => new DespachoHuevoResumenApi(
                Guid.NewGuid(), "Despachado", new(2025, 11, 2), 10, 2950, 1602.75m)).ToList(),
            3);

        var html = await cliente.GetStringAsync("/RecepcionesHuevo?estado=Despachado");

        Assert.Contains("Despachado", html);
        Assert.Contains("02/11/2025", html);
        Assert.Contains("2950", html);
        Assert.Contains("1602.75", html);
        Assert.Contains("Página 1 de 1 con 3 despachos de huevo.", html);
        Assert.Equal("Despachado", aplicacion.Api.UltimosFiltrosDespachosHuevo!.Estado);
    }

    [Fact]
    public async Task DetallesMuestraTablaPorTamanoYLaConfirmacion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Despachado");

        var html = await cliente.GetStringAsync($"/RecepcionesHuevo/{id}");

        Assert.Contains("Primera", html);
        Assert.Contains("Unidades sueltas", html);
        Assert.Contains("1602.75", html);
        Assert.Contains($"\"/RecepcionesHuevo/{id}/ConfirmarRecepcion\"", html);
        Assert.DoesNotContain($"/RecepcionesHuevo/{id}/Recibo", html);
    }

    [Fact]
    public async Task DetallesDeUnRecibidoOfreceElReciboSinConfirmacion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");

        var html = await cliente.GetStringAsync($"/RecepcionesHuevo/{id}");

        Assert.Contains($"\"/RecepcionesHuevo/{id}/Recibo\"", html);
        Assert.DoesNotContain("ConfirmarRecepcion", html);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        aplicacion.Api.ErrorDeObtenerDespachoHuevo = new ErrorApiException(
            404, "Recurso no encontrado");

        var respuesta = await cliente.GetAsync($"/RecepcionesHuevo/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConfirmarRecepcionPideConfirmacionYAlConfirmarNotifica()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Despachado");

        var pantalla = await cliente.GetStringAsync($"/RecepcionesHuevo/{id}/ConfirmarRecepcion");
        Assert.Contains("Confirmar recepción", pantalla);
        Assert.Contains("2950", pantalla);

        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(
            cliente, $"/RecepcionesHuevo/{id}/ConfirmarRecepcion");
        var respuesta = await cliente.PostAsync($"/RecepcionesHuevo/{id}/ConfirmarRecepcion",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/RecepcionesHuevo/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesConfirmarRecepcion);
        Assert.Equal(id, aplicacion.Api.UltimaRecepcionConfirmada);

        // Al seguir la redirección, el detalle (ya Recibido) muestra el éxito
        // y ofrece el recibo.
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");
        var detalle = await cliente.GetStringAsync(respuesta.Headers.Location!.OriginalString);
        Assert.Contains("La recepción quedó confirmada; el emisor fue notificado.", detalle);
        Assert.Contains($"\"/RecepcionesHuevo/{id}/Recibo\"", detalle);
    }

    [Fact]
    public async Task ConfirmarRecepcionDeUnNoDespachadoRedirigeAlDetalleSinLlamarALaApi()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");

        var respuesta = await cliente.GetAsync($"/RecepcionesHuevo/{id}/ConfirmarRecepcion");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/RecepcionesHuevo/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(0, aplicacion.Api.VecesConfirmarRecepcion);
    }

    [Fact]
    public async Task ConfirmarRecepcionConConflictoMuestraElError()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Despachado");
        aplicacion.Api.ErrorDeConfirmarRecepcion = new ErrorApiException(
            409, "Conflicto con el estado actual");
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(
            cliente, $"/RecepcionesHuevo/{id}/ConfirmarRecepcion");

        var respuesta = await cliente.PostAsync($"/RecepcionesHuevo/{id}/ConfirmarRecepcion",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var detalle = await cliente.GetStringAsync(respuesta.Headers.Location!.OriginalString);
        Assert.Contains("Conflicto con el estado actual", detalle);
    }

    [Fact]
    public async Task UnPostSinTokenAntiforgerySeRechaza()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();

        var respuesta = await cliente.PostAsync($"/RecepcionesHuevo/{id}/ConfirmarRecepcion",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.False(respuesta.IsSuccessStatusCode);
        Assert.Equal(0, aplicacion.Api.VecesConfirmarRecepcion);
    }

    [Fact]
    public async Task ReciboDevuelveElPdfDeUnRecibido()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DespachoHuevoActual = ApiIcarusFalsa.CrearDespachoHuevo(id, "Recibido");

        var respuesta = await cliente.GetAsync($"/RecepcionesHuevo/{id}/Recibo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(id, aplicacion.Api.UltimoReciboHuevo);
    }

    [Fact]
    public async Task ReciboInexistenteDevuelve404()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        aplicacion.Api.ErrorDeReciboHuevo = new ErrorApiException(404, "Recurso no encontrado");

        var respuesta = await cliente.GetAsync($"/RecepcionesHuevo/{Guid.NewGuid()}/Recibo");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task MarcarNotificacionLeidaVuelveALaBandeja()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var notificacionId = Guid.NewGuid();
        // CreditoInsuficiente es la única notificación que la bandeja global
        // de CAISY puede recibir realmente: se crea con ClienteId nulo
        // (bandeja global), igual que la consulta del backend filtra acá.
        // DespachoRecibido se crea con el ClienteId del tenant emisor y solo
        // aparece en su propia bandeja de la PWA, nunca en esta.
        aplicacion.Api.NotificacionesDeDespachosHuevo = new BandejaNotificacionesDespachoHuevoApi(
            [new NotificacionDespachoHuevoApi(
                notificacionId, "CreditoInsuficiente", null,
                new(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc), false, null)],
            1);

        var bandeja = await cliente.GetStringAsync("/RecepcionesHuevo");
        Assert.Contains("Crédito insuficiente", bandeja);

        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/RecepcionesHuevo");
        var respuesta = await cliente.PostAsync($"/RecepcionesHuevo/Notificaciones/{notificacionId}/MarcarLeida",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, aplicacion.Api.VecesMarcarLeidaHuevo);
        Assert.Equal(notificacionId, aplicacion.Api.UltimaNotificacionHuevoMarcada);
    }

    [Fact]
    public async Task ElMenuMuestraRecepcionDeHuevoSoloConLaFuncionalidad()
    {
        using var aplicacion = new AplicacionDePruebas();
        var soloHuevo = await aplicacion.AccederAsync(funcCaisy: 2);

        var htmlHuevo = await soloHuevo.GetStringAsync("/RecepcionesHuevo");
        Assert.Contains("Recepción de huevo", htmlHuevo);
        Assert.DoesNotContain("Pedidos de alimento", htmlHuevo);

        using var otraAplicacion = new AplicacionDePruebas();
        var soloAlimento = await otraAplicacion.AccederAsync(funcCaisy: 1);

        var htmlAlimento = await soloAlimento.GetStringAsync("/Pedidos");
        Assert.Contains("Pedidos de alimento", htmlAlimento);
        Assert.DoesNotContain("Recepción de huevo", htmlAlimento);
    }
}
