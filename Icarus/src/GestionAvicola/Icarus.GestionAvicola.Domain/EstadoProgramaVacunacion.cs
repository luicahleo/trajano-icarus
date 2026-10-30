namespace Icarus.GestionAvicola.Domain;

// Estados del programa de vacunación (spec 2026-10-30-gestor-veterinario-
// vacunacion). Borrador es editable y eliminable; Publicado es inmutable y es
// el único asignable mientras siga activo. Desactivado no es un estado: es el
// borrado lógico (EstaActivo) de un publicado. Valores estables porque se
// persisten como entero.
public enum EstadoProgramaVacunacion
{
    Borrador = 0,
    Publicado = 1,
}
