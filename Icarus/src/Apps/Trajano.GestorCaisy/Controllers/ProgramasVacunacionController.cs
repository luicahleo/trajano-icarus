using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Controllers;

// Programas de vacunación (spec 2026-10-30-gestor-veterinario-vacunacion):
// catálogo global reservado a las cuentas CAISY con GestorVeterinario. Ciclo
// Borrador -> Publicado: el borrador se edita, recibe el cronograma por Excel
// (errores por fila) y se elimina; el publicado es inmutable y solo se
// desactiva. Las acciones irreversibles piden confirmación en su propia
// página. El gestor nunca ve galpones, clientes ni tareas.
[Route("ProgramasVacunacion")]
[Authorize(Policy = ConstantesAutorizacion.PoliticaGestorVeterinario)]
public sealed class ProgramasVacunacionController(IApiIcarusClient api) : Controller
{
    private const string MensajeNombreEnUso =
        "Ya existe un programa con ese nombre, incluidos los eliminados o desactivados. Use otro nombre.";

    private const string MensajeBorradorEliminado = "El borrador se eliminó y ya no figura en el catálogo.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) =>
        View(VistaProgramasVacunacion.Crear(await api.ListarProgramasVacunacionAsync(token)));

    [HttpGet("Nuevo")]
    public IActionResult Nuevo() => View(new FormularioProgramaVacunacionVista());

    [HttpPost("Nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(FormularioProgramaVacunacionVista formulario, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return View(formulario);
        try
        {
            var id = await api.CrearProgramaVacunacionAsync(formulario.ADatosApi(), token);
            TempData["Exito"] = "El programa se creó como borrador; importe su cronograma antes de publicarlo.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status409Conflict)
        {
            ModelState.AddModelError(nameof(formulario.Nombre), MensajeNombreEnUso);
            return View(formulario);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status400BadRequest)
        {
            CopiarErroresDeValidacion(error);
            return View(formulario);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalles(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return View(VistaDetallesProgramaVacunacion.Crear(await api.ObtenerProgramaVacunacionAsync(id, token)));
    }

    [HttpGet("{id:guid}/Editar")]
    public async Task<IActionResult> Editar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        var vista = VistaDetallesProgramaVacunacion.Crear(await api.ObtenerProgramaVacunacionAsync(id, token));
        if (!vista.PuedeEditarse)
            return RedirectToAction(nameof(Detalles), new { id });
        return View(FormularioProgramaVacunacionVista.Desde(vista.Programa));
    }

    [HttpPost("{id:guid}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        Guid id, FormularioProgramaVacunacionVista formulario, CancellationToken token)
    {
        formulario.ProgramaId = id;
        if (!ModelState.IsValid)
            return View(formulario);
        try
        {
            await api.ActualizarProgramaVacunacionAsync(id, formulario.ADatosApi(), token);
            TempData["Exito"] = "Los datos del borrador se guardaron.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status409Conflict)
        {
            ModelState.AddModelError(nameof(formulario.Nombre), MensajeNombreEnUso);
            return View(formulario);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status400BadRequest)
        {
            CopiarErroresDeValidacion(error);
            return View(formulario);
        }
    }

    [HttpGet("{id:guid}/Cronograma")]
    public async Task<IActionResult> Cronograma(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await VistaSiAsync(id, vista => vista.PuedeEditarse, token);
    }

    // Importación atómica (spec SP7): si una fila es inválida la API no guarda nada y
    // devuelve un mensaje por fila, que se muestra en el resumen de validación.
    [HttpPost("{id:guid}/Cronograma")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cronograma(Guid id, IFormFile? archivo, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        if (archivo is null || archivo.Length == 0)
        {
            ModelState.AddModelError("archivo", "Adjunte el archivo Excel del cronograma.");
            return await VistaCronogramaAsync(id, token);
        }
        try
        {
            await using var contenido = archivo.OpenReadStream();
            var importados = await api.ImportarCronogramaVacunacionAsync(id, contenido, archivo.FileName, token);
            TempData["Exito"] = $"Se importaron {importados} ítems del cronograma.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status413PayloadTooLarge)
        {
            ModelState.AddModelError(string.Empty, "El archivo supera el tamaño máximo permitido (5 MB).");
            return await VistaCronogramaAsync(id, token);
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status400BadRequest)
        {
            CopiarErroresDeValidacion(error);
            return await VistaCronogramaAsync(id, token);
        }
    }

    [HttpGet("{id:guid}/Publicar")]
    public async Task<IActionResult> ConfirmarPublicacion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await VistaSiAsync(id, vista => vista.PuedePublicarse, token);
    }

    [HttpPost("{id:guid}/Publicar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.PublicarProgramaVacunacionAsync(id, token);
            TempData["Exito"] = "El programa quedó publicado: los clientes ya pueden asignarlo a sus galpones.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = "No se pudo publicar: el programa debe ser un borrador con cronograma importado.";
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    [HttpGet("{id:guid}/Eliminar")]
    public async Task<IActionResult> ConfirmarEliminacion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await VistaSiAsync(id, vista => vista.PuedeEliminarse, token);
    }

    [HttpPost("{id:guid}/Eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.EliminarBorradorProgramaVacunacionAsync(id, token);
            TempData["Exito"] = MensajeBorradorEliminado;
            return RedirectToAction(nameof(Index));
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = "Solo un borrador se puede eliminar; un programa publicado se desactiva.";
            return RedirectToAction(nameof(Detalles), new { id });
        }
        catch (ErrorApiException error) when (error.Estado == StatusCodes.Status404NotFound)
        {
            // Idempotencia: un reintento tras eliminar ya no encuentra el
            // borrador y la API responde 404; se trata como éxito ya aplicado.
            TempData["Exito"] = MensajeBorradorEliminado;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("{id:guid}/Desactivar")]
    public async Task<IActionResult> ConfirmarDesactivacion(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        return await VistaSiAsync(id, vista => vista.PuedeDesactivarse, token);
    }

    [HttpPost("{id:guid}/Desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken token)
    {
        if (!ModelState.IsValid)
            return BadRequest();
        try
        {
            await api.DesactivarProgramaVacunacionAsync(id, token);
            TempData["Exito"] =
                "El programa quedó inactivo: ya no es asignable y sus tareas pendientes se retiraron de los galpones.";
        }
        catch (ErrorApiException error) when (error.Estado is 400 or 409)
        {
            TempData["Error"] = "Solo un programa publicado y activo se puede desactivar.";
        }
        return RedirectToAction(nameof(Detalles), new { id });
    }

    // Páginas de acción y de confirmación: solo se muestran si el ciclo del
    // programa permite la acción; en otro caso vuelven al detalle.
    private async Task<IActionResult> VistaSiAsync(
        Guid id, Func<VistaDetallesProgramaVacunacion, bool> permitida, CancellationToken token)
    {
        var vista = VistaDetallesProgramaVacunacion.Crear(await api.ObtenerProgramaVacunacionAsync(id, token));
        if (!permitida(vista))
            return RedirectToAction(nameof(Detalles), new { id });
        return View(vista);
    }

    private async Task<IActionResult> VistaCronogramaAsync(Guid id, CancellationToken token) =>
        View(nameof(Cronograma),
            VistaDetallesProgramaVacunacion.Crear(await api.ObtenerProgramaVacunacionAsync(id, token)));

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
