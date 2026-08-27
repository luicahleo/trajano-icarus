namespace Icarus.GestionAvicola.Domain;

// Destinos de una tarea de vacunación (spec SP7): nace Pendiente y se cierra
// Completada o Cancelada; sin reprogramación individual (si el plan cambia,
// se corrige el plan o se reasigna el galpón).
public enum EstadoTareaVacunacion
{
    Pendiente,
    Completada,
    Cancelada,
}
