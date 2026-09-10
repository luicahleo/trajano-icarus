using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Publicaciones de Precio de Huevo (SP9A): bandeja global reservada a las
// cuentas CAISY con GestorRecepcionHuevos. Lista e historial, importación del
// Excel original, revisión del borrador, publicación con confirmación
// explícita, descarte lógico de un borrador, anulación de una publicación
// futura y descarga del original. El documento original es un Excel (.xlsx).
[Route("PreciosHuevo")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorRecepcionHuevos)]
public sealed class PreciosHuevoController(IApiIcarusClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(await api.ListarPublicacionesHuevoAsync(token));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        PublicacionPrecioHuevoDetalleApi publicacion;
        try
        {
            publicacion = await api.ObtenerPublicacionHuevoAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return View(VistaDetallesHuevo.Crear(publicacion));
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
            ModelState.AddModelError("archivo", "Adjunte el archivo Excel de la publicación.");
            return View();
        }
        try
        {
            await using var contenido = archivo.OpenReadStream();
            var id = await api.ImportarExcelHuevoAsync(contenido, archivo.FileName, token);
            TempData["Exito"] = "El Excel se importó como borrador; revísalo antes de publicar.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status413PayloadTooLarge)
        {
            ModelState.AddModelError(
                string.Empty, "El archivo supera el tamaño máximo permitido (5 MB).");
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
        var publicacion = await api.ObtenerPublicacionHuevoAsync(id, token);
        if (publicacion.Estado != "Borrador")
            return RedirectToAction(nameof(Detalles), new { id });
        return View(new FormularioBorradorHuevoVista
        {
            PublicacionId = id,
            FechaNotificacion = publicacion.FechaNotificacion,
            FechaVigencia = publicacion.FechaVigencia,
            Servicio = publicacion.Servicio,
            Detalles = publicacion.Detalles.Select(d => new FilaDetalleHuevoVista
            {
                Tamano = d.Tamano,
                PrecioAlProductor = d.PrecioAlProductor,
                PrecioActualDocumento = d.PrecioActualDocumento,
            }).ToList(),
        });
    }

    [HttpPost("{id:guid}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid id, FormularioBorradorHuevoVista formulario, CancellationToken token)
    {
        formulario.PublicacionId = id;
        if (!ModelState.IsValid)
            return View(formulario);
        var comando = new ComandoActualizarBorradorHuevoApi(
            id, formulario.FechaNotificacion, formulario.FechaVigencia, formulario.Servicio,
            formulario.Detalles.Select(d => new DatosDetalleHuevoApi(
                d.Tamano, d.PrecioAlProductor, d.PrecioActualDocumento)).ToList());
        try
        {
            await api.ActualizarBorradorHuevoAsync(comando, token);
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
        var publicacion = await api.ObtenerPublicacionHuevoAsync(id, token);
        return View(VistaDetallesHuevo.Crear(publicacion));
    }

    [HttpPost("{id:guid}/Publicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.PublicarHuevoAsync(id, token);
            TempData["Exito"] = "La publicación quedó publicada.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(ConfirmarPublicacion), new { id });
        }
    }

    [HttpGet("{id:guid}/Corregir")]
    public async Task<IActionResult> Corregir(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var correctiva = await api.ObtenerPublicacionHuevoAsync(id, token);
        if (correctiva.Estado != "Borrador")
            return RedirectToAction(nameof(Detalles), new { id });
        var vigente = await api.ObtenerPublicacionVigenteHuevoAsync(token);
        if (vigente is null)
            return RedirectToAction(nameof(Detalles), new { id });
        var previa = await api.PrevisualizarCorreccionHuevoAsync(vigente.Id, id, token);
        return View(new VistaCorregirHuevo(vigente, correctiva, previa, new FormularioCorregirHuevoVista { CorrectivaId = id }));
    }

    [HttpPost("{id:guid}/Corregir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Corregir(Guid id, FormularioCorregirHuevoVista formulario, CancellationToken token)
    {
        formulario.CorrectivaId = id;
        var vigente = await api.ObtenerPublicacionVigenteHuevoAsync(token);
        if (vigente is null)
            return RedirectToAction(nameof(Detalles), new { id });
        if (!ModelState.IsValid)
        {
            var correctivaInvalida = await api.ObtenerPublicacionHuevoAsync(id, token);
            var previaInvalida = await api.PrevisualizarCorreccionHuevoAsync(vigente.Id, id, token);
            return View(new VistaCorregirHuevo(vigente, correctivaInvalida, previaInvalida, formulario));
        }
        try
        {
            await api.CorregirVigenteHuevoAsync(vigente.Id, id, formulario.Motivo, token);
            TempData["Exito"] = "La publicación se corrigió y los ajustes de crédito quedaron aplicados.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(Corregir), new { id });
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
            await api.AnularFuturaHuevoAsync(id, token);
            TempData["Exito"] = "La publicación futura quedó anulada.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    // El descarte de un borrador exige una confirmación en su propia página:
    // el borrador desaparece del historial y no se puede recuperar.
    [HttpGet("{id:guid}/Descartar")]
    [ActionName("ConfirmarDescartar")]
    public async Task<IActionResult> ConfirmarDescartar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var publicacion = await api.ObtenerPublicacionHuevoAsync(id, token);
        if (publicacion.Estado != "Borrador")
            return RedirectToAction(nameof(Detalles), new { id });
        return View("Descartar", VistaDetallesHuevo.Crear(publicacion));
    }

    [HttpPost("{id:guid}/Descartar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Descartar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.DescartarBorradorHuevoAsync(id, token);
            TempData["Exito"] = "El borrador se descartó y ya no figura en el historial.";
            return RedirectToAction(nameof(Index));
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = error.MensajeParaLaInterfaz();
            return RedirectToAction(nameof(ConfirmarDescartar), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            // Idempotencia: un reintento tras descartar encuentra el borrador
            // inactivo y la API responde 404; se trata como éxito ya aplicado.
            TempData["Exito"] = "El borrador se descartó y ya no figura en el historial.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("{id:guid}/DocumentoOriginal")]
    public async Task<IActionResult> DocumentoOriginal(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var publicacion = await api.ObtenerPublicacionHuevoAsync(id, token);
        Stream contenido;
        try
        {
            contenido = await api.DescargarDocumentoOriginalHuevoAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        return File(
            contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"precios-huevo-{publicacion.FechaNotificacion:yyyy-MM-dd}.xlsx");
    }

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
