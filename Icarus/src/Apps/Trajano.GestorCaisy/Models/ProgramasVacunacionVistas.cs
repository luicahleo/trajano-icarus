using System.ComponentModel.DataAnnotations;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Estado visible del programa (spec 2026-10-30): un publicado desactivado se
// muestra como «Inactivo». Los borradores eliminados nunca llegan: la API no
// los devuelve. El chip siempre lleva texto, no solo color.
public static class EstadoVisiblePrograma
{
    public static string Etiqueta(string estado, bool estaActivo) => estaActivo ? estado : "Inactivo";

    public static string ClaseChip(string estado, bool estaActivo) =>
        $"chip--{Etiqueta(estado, estaActivo).ToLowerInvariant()}";
}

public sealed record FilaProgramaVacunacionVista(
    ProgramaVacunacionGestionResumenApi Programa, string Etiqueta, string ClaseChip);

public sealed record VistaProgramasVacunacion(IReadOnlyList<FilaProgramaVacunacionVista> Programas)
{
    // Orden de trabajo: borradores, publicados activos e inactivos; dentro de
    // cada grupo, por nombre.
    public static VistaProgramasVacunacion Crear(IReadOnlyList<ProgramaVacunacionGestionResumenApi> programas) =>
        new(programas
            .OrderBy(Orden)
            .ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(p => new FilaProgramaVacunacionVista(
                p,
                EstadoVisiblePrograma.Etiqueta(p.Estado, p.EstaActivo),
                EstadoVisiblePrograma.ClaseChip(p.Estado, p.EstaActivo)))
            .ToList());

    private static int Orden(ProgramaVacunacionGestionResumenApi programa)
    {
        if (!programa.EstaActivo)
            return 2;
        return programa.Estado == "Borrador" ? 0 : 1;
    }
}

// Acciones habilitadas según el ciclo: solo el borrador activo se edita,
// recibe cronograma y se elimina; publicar exige cronograma; solo un
// publicado activo se desactiva.
public sealed record VistaDetallesProgramaVacunacion(
    ProgramaVacunacionGestionDetalleApi Programa,
    string Etiqueta,
    string ClaseChip,
    bool PuedeEditarse,
    bool PuedePublicarse,
    bool PuedeDesactivarse)
{
    public static VistaDetallesProgramaVacunacion Crear(ProgramaVacunacionGestionDetalleApi programa)
    {
        var esBorrador = programa.EstaActivo && programa.Estado == "Borrador";
        return new VistaDetallesProgramaVacunacion(
            programa,
            EstadoVisiblePrograma.Etiqueta(programa.Estado, programa.EstaActivo),
            EstadoVisiblePrograma.ClaseChip(programa.Estado, programa.EstaActivo),
            PuedeEditarse: esBorrador,
            PuedePublicarse: esBorrador && programa.Items.Count > 0,
            PuedeDesactivarse: programa.EstaActivo && programa.Estado == "Publicado");
    }

    // Solo el borrador activo se elimina; coincide con la edición del ciclo.
    public bool PuedeEliminarse => PuedeEditarse;
}

public sealed class FormularioProgramaVacunacionVista
{
    public Guid? ProgramaId { get; set; }

    [Required(ErrorMessage = "Escriba el nombre del programa.")]
    [StringLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indique la cantidad de aves.")]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad de aves debe ser mayor que cero.")]
    public int? CantidadAves { get; set; }

    [StringLength(1000, ErrorMessage = "Las observaciones admiten hasta 1000 caracteres.")]
    public string? Observaciones { get; set; }

    public DatosProgramaVacunacionApi ADatosApi() =>
        new(Nombre.Trim(), CantidadAves.GetValueOrDefault(),
            string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim());

    public static FormularioProgramaVacunacionVista Desde(ProgramaVacunacionGestionDetalleApi programa) => new()
    {
        ProgramaId = programa.Id,
        Nombre = programa.Nombre,
        CantidadAves = programa.CantidadAves,
        Observaciones = programa.Observaciones,
    };
}
