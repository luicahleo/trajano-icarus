using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Datos de entrada de un ítem del cronograma (spec SP7). Fecha es la fecha
// programada que trae la columna FECHA del Excel; si la fila no la trae, queda
// null y la tarea usa el FechaNacimientoLote del galpón al asignar.
public sealed record DatosItemPlanVacunacion(
    int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones, DateOnly? Fecha = null);

// Ítem del cronograma (spec SP7): "a los N días de edad del lote, aplicar X".
// Vacuna es texto libre: también cubre los manejos del papel de CAISY
// (desparasitación, recorte de pico, traslado). Hija del agregado
// ProgramaVacunacion: solo se crea y desactiva a través de la raíz.
public sealed class ItemPlanVacunacion : Entity
{
    private ItemPlanVacunacion()
    {
    }

    internal ItemPlanVacunacion(
        int edadDia, string vacuna, string? modoAplicacion, string? observaciones, DateOnly? fecha = null)
    {
        if (edadDia <= 0)
            throw new ReglaNegocioException("La edad en días debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(vacuna))
            throw new ReglaNegocioException("La vacuna del ítem es obligatoria.");

        Id = Guid.NewGuid();
        EdadDia = edadDia;
        Vacuna = vacuna.Trim();
        ModoAplicacion = string.IsNullOrWhiteSpace(modoAplicacion) ? null : modoAplicacion.Trim();
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        Fecha = fecha;
        EstaActivo = true;
    }

    public int EdadDia { get; private set; }

    public string Vacuna { get; private set; } = string.Empty;

    public string? ModoAplicacion { get; private set; }

    public string? Observaciones { get; private set; }

    // Fecha programada de la fila del Excel (columna FECHA). Null si el
    // archivo no la traía: entonces la tarea usa FechaNacimientoLote + EdadDia.
    public DateOnly? Fecha { get; private set; }

    public bool EstaActivo { get; private set; }

    internal void Desactivar() => EstaActivo = false;
}
