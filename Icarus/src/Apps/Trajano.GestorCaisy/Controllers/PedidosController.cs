using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Bandeja de pedidos de alimento entrantes (SP8B): listado global con filtros
// y paginación, detalle con los precios congelados e historial, y las cuatro
// decisiones de CAISY (devolver, rechazar, aceptar y cambiar la entrega
// estimada), cada una con su confirmación propia. CAISY nunca altera tipos ni
// cantidades. Los motivos no se registran en los logs (anti-PII).
[Route("Pedidos")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorPedidoAlimento)]
public sealed class PedidosController(IApiIcarusClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] FiltrosPedidosVista filtros, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var pagina = await api.ListarPedidosAsync(
            new FiltrosPedidosApi(filtros.Estado, filtros.Presentacion,
                filtros.Pagina, filtros.TamanoPagina), token);
        var notificaciones = await api.ListarNotificacionesPedidoAsync(token);
        return View(new BandejaPedidosVista(
            pagina, filtros.Estado, filtros.Presentacion, notificaciones));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        PedidoDetalleApi pedido;
        try
        {
            pedido = await api.ObtenerPedidoAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return View(new VistaPedidoDetalle(
            pedido,
            PuedeProcesarse: pedido.Estado == "Solicitado",
            PuedeActualizarEntrega: pedido.Estado == "Aceptado"));
    }

    [HttpGet("{id:guid}/Devolver")]
    [ActionName("Devolver")]
    public async Task<IActionResult> ConfirmarDevolucion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await PantallaDecisionConMotivo(
            id, "La devolución es una decisión no terminal: el pedido vuelve al borrador del tenant con su historial intacto.", token);
    }

    [HttpGet("{id:guid}/Rechazar")]
    [ActionName("Rechazar")]
    public async Task<IActionResult> ConfirmarRechazo(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await PantallaDecisionConMotivo(
            id, "El rechazo es definitivo: el pedido termina en el estado Rechazado y no admite más transiciones.", token);
    }

    // Ambas confirmaciones comparten la pantalla del motivo; el aviso cambia
    // porque la consecuencia de cada decisión no es la misma.
    private async Task<IActionResult> PantallaDecisionConMotivo(
        Guid id, string aviso, CancellationToken token)
    {
        ViewData["Aviso"] = aviso;
        var pedido = await api.ObtenerPedidoAsync(id, token);
        if (pedido.Estado != "Solicitado")
            return RedirectToAction(nameof(Detalles), new { id });
        return View("DecisionMotivo", new FormularioMotivoVista { Id = id });
    }

    [HttpPost("{id:guid}/Devolver")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Devolver(
        Guid id, FormularioMotivoVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return View("DecisionMotivo", formulario);
        try
        {
            await api.DevolverPedidoAsync(id, formulario.Motivo, token);
            TempData["Exito"] = "El pedido volvió al borrador del tenant para su corrección.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            ModelState.AddModelError(string.Empty, error.MensajeParaLaInterfaz());
            return View("DecisionMotivo", formulario);
        }
    }

    [HttpPost("{id:guid}/Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(
        Guid id, FormularioMotivoVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return View("DecisionMotivo", formulario);
        try
        {
            await api.RechazarPedidoAsync(id, formulario.Motivo, token);
            TempData["Exito"] = "El pedido quedó rechazado de forma definitiva.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            ModelState.AddModelError(string.Empty, error.MensajeParaLaInterfaz());
            return View("DecisionMotivo", formulario);
        }
    }

    [HttpGet("{id:guid}/Aceptar")]
    [ActionName("Aceptar")]
    public async Task<IActionResult> ConfirmarAceptacion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var pedido = await api.ObtenerPedidoAsync(id, token);
        if (pedido.Estado != "Solicitado")
            return RedirectToAction(nameof(Detalles), new { id });
        return View(new FormularioEntregaVista
        {
            Id = id,
            FechaEntregaEstimada = FechasDeOficina.Hoy(),
        });
    }

    [HttpPost("{id:guid}/Aceptar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aceptar(
        Guid id, FormularioEntregaVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return View("Aceptar", formulario);
        if (formulario.FechaEntregaEstimada < FechasDeOficina.Hoy())
        {
            ModelState.AddModelError(
                nameof(formulario.FechaEntregaEstimada),
                "La fecha de entrega estimada debe ser hoy o posterior.");
            return View("Aceptar", formulario);
        }
        try
        {
            await api.AceptarPedidoAsync(id, formulario.FechaEntregaEstimada, token);
            TempData["Exito"] = "El pedido quedó aceptado con la entrega estimada indicada.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            ModelState.AddModelError(string.Empty, error.MensajeParaLaInterfaz());
            return View("Aceptar", formulario);
        }
    }

    [HttpGet("{id:guid}/EntregaEstimada")]
    [ActionName("EntregaEstimada")]
    public async Task<IActionResult> ConfirmarEntregaEstimada(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var pedido = await api.ObtenerPedidoAsync(id, token);
        if (pedido.Estado != "Aceptado")
            return RedirectToAction(nameof(Detalles), new { id });
        return View(new FormularioEntregaVista
        {
            Id = id,
            FechaEntregaEstimada = pedido.FechaEntregaEstimada ?? FechasDeOficina.Hoy(),
        });
    }

    [HttpPost("{id:guid}/EntregaEstimada")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EntregaEstimada(
        Guid id, FormularioEntregaVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return View("EntregaEstimada", formulario);
        if (formulario.FechaEntregaEstimada < FechasDeOficina.Hoy())
        {
            ModelState.AddModelError(
                nameof(formulario.FechaEntregaEstimada),
                "La fecha de entrega estimada debe ser hoy o posterior.");
            return View("EntregaEstimada", formulario);
        }
        try
        {
            await api.ActualizarEntregaEstimadaAsync(id, formulario.FechaEntregaEstimada, token);
            TempData["Exito"] = "La entrega estimada se actualizó y el tenant fue notificado.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            ModelState.AddModelError(string.Empty, error.MensajeParaLaInterfaz());
            return View("EntregaEstimada", formulario);
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
            await api.MarcarNotificacionPedidoLeidaAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 404 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Index));
    }
}
