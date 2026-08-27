using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Tarea materializada al asignar un plan a un galpón (spec SP7): copia el
// snapshot del ítem (el catálogo puede cambiar y el historial sanitario no).
// ClienteId va desnormalizado para el filtro de tenant sin join, patrón de
// SP5/SP6. Completar registra lo que pasó (fecha informada por el usuario,
// nunca futura); cancelar es decisión del cliente. CompletadaPor guarda el id
// del usuario, no el nombre (anti-PII).
public sealed class TareaVacunacion : AggregateRoot
{
    private TareaVacunacion()
    {
    }

    public TareaVacunacion(
        Guid galponId, Guid clienteId, Guid programaVacunacionId, Guid itemPlanVacunacionId,
        int edadDia, string vacuna, string? modoAplicacion, string? observacionesProgramadas,
        DateOnly fechaProgramada)
    {
        if (galponId == Guid.Empty)
            throw new ReglaNegocioException("La tarea debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La tarea debe pertenecer a un cliente.");
        if (edadDia <= 0)
            throw new ReglaNegocioException("La edad en días debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(vacuna))
            throw new ReglaNegocioException("La vacuna de la tarea es obligatoria.");

        GalponId = galponId;
        ClienteId = clienteId;
        ProgramaVacunacionId = programaVacunacionId;
        ItemPlanVacunacionId = itemPlanVacunacionId;
        EdadDia = edadDia;
        Vacuna = vacuna.Trim();
        ModoAplicacion = string.IsNullOrWhiteSpace(modoAplicacion) ? null : modoAplicacion.Trim();
        ObservacionesProgramadas = string.IsNullOrWhiteSpace(observacionesProgramadas) ? null : observacionesProgramadas.Trim();
        FechaProgramada = fechaProgramada;
        Estado = EstadoTareaVacunacion.Pendiente;
        EstaActivo = true;
    }

    // Para tests que necesitan ids fijos.
    public TareaVacunacion(
        Guid id, Guid galponId, Guid clienteId, Guid programaVacunacionId, Guid itemPlanVacunacionId,
        int edadDia, string vacuna, string? modoAplicacion, string? observacionesProgramadas,
        DateOnly fechaProgramada)
        : this(galponId, clienteId, programaVacunacionId, itemPlanVacunacionId,
            edadDia, vacuna, modoAplicacion, observacionesProgramadas, fechaProgramada) => Id = id;

    public Guid GalponId { get; private set; }

    public Guid ClienteId { get; private set; }

    public Guid ProgramaVacunacionId { get; private set; }

    public Guid ItemPlanVacunacionId { get; private set; }

    public int EdadDia { get; private set; }

    public string Vacuna { get; private set; } = string.Empty;

    public string? ModoAplicacion { get; private set; }

    public string? ObservacionesProgramadas { get; private set; }

    public DateOnly FechaProgramada { get; private set; }

    public EstadoTareaVacunacion Estado { get; private set; }

    public DateOnly? FechaAplicacion { get; private set; }

    public int? AvesVacunadas { get; private set; }

    public Guid? CompletadaPor { get; private set; }

    public string? ObservacionesAplicacion { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public bool EstaActivo { get; private set; }

    public void Completar(DateOnly fechaAplicacion, int? avesVacunadas, Guid completadaPor, string? observaciones)
    {
        ExigirPendiente();
        if (fechaAplicacion > Hoy())
            throw new ReglaNegocioException("La fecha de aplicación no puede ser futura.");
        if (avesVacunadas is <= 0)
            throw new ReglaNegocioException("Las aves vacunadas deben ser mayores que cero.");
        if (completadaPor == Guid.Empty)
            throw new ReglaNegocioException("La aplicación debe registrar el usuario que la informó.");

        FechaAplicacion = fechaAplicacion;
        AvesVacunadas = avesVacunadas;
        CompletadaPor = completadaPor;
        ObservacionesAplicacion = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        Estado = EstadoTareaVacunacion.Completada;
    }

    public void Cancelar(string? motivo)
    {
        ExigirPendiente();
        MotivoCancelacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        Estado = EstadoTareaVacunacion.Cancelada;
    }

    // Soft delete (glosario): al reasignar o quitar el plan se desactivan las
    // pendientes; las completadas y canceladas quedan como historial sanitario.
    public void Desactivar() => EstaActivo = false;

    private void ExigirPendiente()
    {
        if (Estado != EstadoTareaVacunacion.Pendiente)
            throw new ReglaNegocioException("La tarea ya está cerrada.");
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
