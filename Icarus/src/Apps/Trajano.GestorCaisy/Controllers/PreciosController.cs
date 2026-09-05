using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Notificaciones de Precios de Alimentos (SP8A): lista e historial,
// importación del PDF, revisión del borrador, publicación con confirmación
// explícita, anulación de una publicación futura y descarga del original.
// Los pedidos de alimento llegan en SP8B; no se muestran controles de pedidos.
[Route("Precios")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorPedidoAlimento)]
public sealed class PreciosController(IApiIcarusClient api) : Controller
{
    [HttpGet("~/")]
    [HttpGet("~/Precios")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await api.ListarNotificacionesAsync(token));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        NotificacionPreciosDetalleApi notificacion;
        try
        {
            notificacion = await api.ObtenerNotificacionAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return View(new VistaDetalles(
            notificacion,
            PuedeEditarse: notificacion.Estado == "Borrador",
            PuedeAnularse: PuedeAnularse(notificacion)));
    }

    [HttpGet("Importar")]
    public IActionResult Importar() => View();

    [HttpPost("Importar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Importar(IFormFile? archivo, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return View();
        if (archivo is null || archivo.Length == 0)
        {
            ModelState.AddModelError("archivo", "Adjunte el archivo PDF de la notificación.");
            return View();
        }
        try
        {
            await using var contenido = archivo.OpenReadStream();
            var id = await api.ImportarPdfAsync(contenido, archivo.FileName, token);
            TempData["Exito"] = "El PDF se importó como borrador; revísalo antes de publicar.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status413PayloadTooLarge)
        {
            ModelState.AddModelError(
                string.Empty, "El archivo supera el tamaño máximo permitido (20 MB).");
            return View();
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status400BadRequest)
        {
            CopiarErroresDeValidacion(error);
            return View();
        }
    }

    [HttpGet("{id:guid}/Editar")]
    public async Task<IActionResult> Editar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var notificacion = await api.ObtenerNotificacionAsync(id, token);
        if (notificacion.Estado != "Borrador")
            return RedirectToAction(nameof(Detalles), new { id });
        return View(new FormularioBorradorVista
        {
            NotificacionId = id,
            FechaDocumento = notificacion.FechaDocumento,
            VigenteDesde = notificacion.VigenteDesde,
            AporteCaisy = notificacion.AporteCaisy,
            Fondo = notificacion.Fondo,
            Servicios = notificacion.Servicios,
            Detalles = notificacion.Detalles.Select(d => new FilaDetalleVista
            {
                TipoAlimento = d.TipoAlimento,
                Presentacion = d.Presentacion,
                PrecioFinalPor40Kg = d.PrecioFinalPor40Kg,
                PrecioActualDocumento = d.PrecioActualDocumento,
                EdadDesdeDias = d.EdadDesdeDias,
                EdadHastaDias = d.EdadHastaDias,
            }).ToList(),
        });
    }

    [HttpPost("{id:guid}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid id, FormularioBorradorVista formulario, CancellationToken token)
    {
        formulario.NotificacionId = id;
        if (!ModelState.IsValid)
            return View(formulario);
        var comando = new ComandoActualizarBorradorApi(
            id, formulario.FechaDocumento, formulario.VigenteDesde,
            formulario.AporteCaisy, formulario.Fondo, formulario.Servicios,
            formulario.Detalles.Select(d => new DatosDetalleApi(
                d.TipoAlimento, d.Presentacion, d.PrecioFinalPor40Kg,
                d.EdadDesdeDias, d.EdadHastaDias, d.PrecioActualDocumento)).ToList());
        try
        {
            await api.ActualizarBorradorAsync(comando, token);
            TempData["Exito"] = "El borrador se guardó.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status409Conflict)
        {
            ModelState.AddModelError(
                string.Empty,
                "Otro usuario modificó el borrador; recárguelo antes de volver a guardar.");
            return View(formulario);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status400BadRequest)
        {
            CopiarErroresDeValidacion(error);
            return View(formulario);
        }
    }

    // La publicación exige una confirmación explícita en su propia página.
    [HttpGet("{id:guid}/Publicar")]
    [ActionName("ConfirmarPublicacion")]
    public async Task<IActionResult> ConfirmarPublicacion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var notificacion = await api.ObtenerNotificacionAsync(id, token);
        return View(new VistaDetalles(
            notificacion,
            PuedeEditarse: false,
            PuedeAnularse: PuedeAnularse(notificacion)));
    }

    [HttpPost("{id:guid}/Publicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.PublicarAsync(id, token);
            TempData["Exito"] = "La notificación quedó publicada.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(ConfirmarPublicacion), new { id });
        }
    }

    [HttpPost("{id:guid}/Anular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.AnularFuturaAsync(id, token);
            TempData["Exito"] = "La publicación futura quedó anulada.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    [HttpGet("{id:guid}/DocumentoOriginal")]
    public async Task<IActionResult> DocumentoOriginal(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var notificacion = await api.ObtenerNotificacionAsync(id, token);
        Stream contenido;
        try
        {
            contenido = await api.DescargarDocumentoOriginalAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return File(
            contenido, "application/pdf",
            $"notificacion-precios-{notificacion.FechaDocumento:yyyy-MM-dd}.pdf");
    }

    private static bool PuedeAnularse(NotificacionPreciosDetalleApi notificacion) =>
        notificacion.Estado == "Publicada"
        && notificacion.VigenteDesde > FechasDeOficina.Hoy();

    private void CopiarErroresDeValidacion(ErrorApiException error)
    {
        if (error.ErroresValidacion is not { } errores)
        {
            ModelState.AddModelError(string.Empty, error.Titulo ?? "La API rechazó la solicitud.");
            return;
        }
        foreach (var (campo, mensajes) in errores)
            foreach (var mensaje in mensajes)
                ModelState.AddModelError(campo, mensaje);
    }
}
