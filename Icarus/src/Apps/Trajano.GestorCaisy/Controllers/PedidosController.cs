using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
            PuedeActualizarEntrega: pedido.Estado == "Aceptado",
            PuedeDespacharse: pedido.Estado == "Aceptado",
            Credito: await CreditoDelPedidoONullAsync(id, token)));
    }

    // Despacho (SP8C "Despacho, nota y recepción"): pantalla con el resumen de
    // lo solicitado, campos manuales de la nota y selección de las imágenes de
    // respaldo (páginas o reverso) que se suben tras registrar la entrega.
    [HttpGet("{id:guid}/Despachar")]
    [ActionName("Despachar")]
    public async Task<IActionResult> ConfirmarDespacho(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var pedido = await api.ObtenerPedidoAsync(id, token);
        if (pedido.Estado != "Aceptado")
            return RedirectToAction(nameof(Detalles), new { id });
        return await VistaConCredito("Despachar", id, FormularioDespachoDesde(pedido), token);
    }

    private static FormularioDespachoVista FormularioDespachoDesde(PedidoDetalleApi pedido) => new()
    {
        Id = pedido.Id,
        FechaNota = FechasDeOficina.Hoy(),
        Lineas = pedido.Lineas.Select(l => new LineaDespachoVista
        {
            TipoAlimento = l.TipoAlimento,
            CantidadSolicitada = l.CantidadSolicitada,
            CantidadEntregada = l.CantidadSolicitada,
        }).ToList(),
    };

    // Registra la entrega/nota (una única por pedido). La foto de la nota la
    // adjunta el receptor al confirmar la recepción (SP8D): CAISY no sube
    // respaldos en este paso.
    [HttpPost("{id:guid}/Despachar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Despachar(
        Guid id, FormularioDespachoVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return await VistaConCredito("Despachar", id, formulario, token);
        if (formulario.Lineas.Any(l => l.CantidadEntregada < 0))
        {
            ModelState.AddModelError(
                string.Empty, "La cantidad entregada no puede ser negativa.");
            return await VistaConCredito("Despachar", id, formulario, token);
        }
        try
        {
            await api.DespacharPedidoAsync(new ComandoDespachoApi(
                id, formulario.NumeroNota.Trim(), formulario.FechaNota,
                formulario.TotalInformado,
                formulario.Lineas.Select(l => new LineaDespachoApi(
                    l.TipoAlimento, l.CantidadEntregada)).ToList()), token);
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            ModelState.AddModelError(string.Empty, error.MensajeParaLaInterfaz());
            return await VistaConCredito("Despachar", id, formulario, token);
        }
        TempData["Exito"] = "El pedido quedó despachado con su nota registrada.";
        return RedirectToAction(nameof(Detalles), new { id });
    }

    // Recibo imprimible (spec SP8D): PDF con los datos del despacho, para
    // firmar/sellar en papel antes de entregarlo al transportista.
    [HttpGet("{id:guid}/Recibo")]
    public async Task<IActionResult> Recibo(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            var contenido = await api.ObtenerReciboPdfAsync(id, token);
            return File(contenido, "application/pdf", "recibo.pdf");
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    // Vista derivada de un respaldo para el detalle (inline); el original no
    // se sirve por acá: la API lo entrega como adjunto autorizado al cliente.
    [HttpGet("{id:guid}/Nota/{documentoId:guid}")]
    public async Task<IActionResult> NotaDocumento(Guid id, Guid documentoId, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            var (contenido, tipoContenido) = await api.DescargarDocumentoNotaAsync(id, documentoId, token);
            return File(contenido, tipoContenido);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
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
        return await VistaConCredito("Aceptar", id, new FormularioEntregaVista
        {
            Id = id,
            FechaEntregaEstimada = FechasDeOficina.Hoy(),
        }, token);
    }

    [HttpPost("{id:guid}/Aceptar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aceptar(
        Guid id, FormularioEntregaVista formulario, CancellationToken token)
    {
        formulario.Id = id;
        if (!ModelState.IsValid)
            return await VistaConCredito("Aceptar", id, formulario, token);
        if (formulario.FechaEntregaEstimada < FechasDeOficina.Hoy())
        {
            ModelState.AddModelError(
                nameof(formulario.FechaEntregaEstimada),
                "La fecha de entrega estimada debe ser hoy o posterior.");
            return await VistaConCredito("Aceptar", id, formulario, token);
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
            return await VistaConCredito("Aceptar", id, formulario, token);
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

    // El crédito es informativo: si el cálculo no está disponible, la
    // pantalla carga igual y la decisión sigue habilitada (spec
    // 2026-09-11-credito-huevo-vista-caisy-design). No se degrada
    // TaskCanceledException: una cancelación real del token significa que la
    // petición se abortó y no hay pantalla que renderizar. Sin logs: el
    // saldo es dato financiero del cliente (anti-PII).
    private async Task<CreditoHuevoPedidoApi?> CreditoDelPedidoONullAsync(
        Guid id, CancellationToken token)
    {
        try
        {
            return await api.ObtenerCreditoDePedidoAsync(id, token);
        }
        catch (ErrorApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            // Un 200 con cuerpo ilegible no pasa por AsegurarExitoAsync:
            // respuesta truncada, o un campo con [JsonRequired] que la API
            // deje de enviar. Degrada igual que el resto.
            return null;
        }
    }

    // Cada render de un formulario de decisión pasa por acá, incluidos los
    // re-render por error de validación: el bloque de crédito no puede
    // desaparecer a mitad de la decisión.
    private async Task<IActionResult> VistaConCredito<T>(
        string vista, Guid id, T formulario, CancellationToken token)
        where T : IFormularioConCredito
    {
        formulario.Credito = await CreditoDelPedidoONullAsync(id, token);
        return View(vista, formulario);
    }
}
