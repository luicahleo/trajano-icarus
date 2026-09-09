using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Bandeja de recepción de huevo (SP9C): mirror simplificado de la bandeja de
// pedidos de alimento. CAISY lista los despachos de huevo despachados por los
// tenants, confirma la recepción del que llegó y descarga el recibo en PDF.
// No hay negociación (devolver, rechazar, aceptar) ni carga de archivos: en
// este flujo CAISY solo confirma.
[Route("RecepcionesHuevo")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorRecepcionHuevos)]
public sealed class RecepcionesHuevoController(IApiIcarusClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] FiltrosDespachosHuevoVista filtros, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var pagina = await api.ListarDespachosHuevoAsync(
            new FiltrosDespachosHuevoApi(filtros.Estado, filtros.Pagina, filtros.TamanoPagina), token);
        var notificaciones = await api.ListarNotificacionesDespachoHuevoAsync(token);
        return View(new BandejaDespachosHuevoVista(
            pagina, filtros.Estado, filtros.Pagina, filtros.TamanoPagina, notificaciones));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        DespachoHuevoDetalleApi despacho;
        try
        {
            despacho = await api.ObtenerDespachoHuevoAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return View(new VistaDespachoHuevoDetalle(
            despacho, PuedeConfirmarse: despacho.Estado == "Despachado"));
    }

    // La confirmación exige una pantalla propia: la recepción es un paso
    // terminal que notifica al emisor y habilita el recibo.
    [HttpGet("{id:guid}/ConfirmarRecepcion")]
    [ActionName("ConfirmarRecepcion")]
    public async Task<IActionResult> ConfirmarRecepcionPantalla(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var despacho = await api.ObtenerDespachoHuevoAsync(id, token);
        if (despacho.Estado != "Despachado")
            return RedirectToAction(nameof(Detalles), new { id });
        return View("ConfirmarRecepcion", new VistaDespachoHuevoDetalle(despacho, PuedeConfirmarse: true));
    }

    [HttpPost("{id:guid}/ConfirmarRecepcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarRecepcion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.ConfirmarRecepcionDespachoHuevoAsync(id, token);
            TempData["Exito"] = "La recepción quedó confirmada; el emisor fue notificado.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    // Recibo de la recepción (spec SP9C): PDF generado por la API; solo existe
    // después de confirmar la recepción.
    [HttpGet("{id:guid}/Recibo")]
    public async Task<IActionResult> Recibo(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            var contenido = await api.ObtenerReciboDespachoHuevoPdfAsync(id, token);
            return File(contenido, "application/pdf", "recibo.pdf");
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    [HttpPost("Notificaciones/{id:guid}/MarcarLeida")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.MarcarNotificacionDespachoHuevoLeidaAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 404 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Index));
    }
}
