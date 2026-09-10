namespace Icarus.GestionAvicola.Domain;

// Estados de una publicación de precio de huevo (spec SP9/SP9D), mismo ciclo
// que EstadoNotificacionPreciosAlimentos: Borrador editable, Publicada
// inmutable y vigente hasta que otra publicación posterior entra en vigor,
// Anulada solo alcanza a publicaciones futuras. Corregida es distinta de
// Anulada: una publicación ya vigente que resultó errónea y fue reemplazada,
// con despachos que ya la usaron y que hay que reconciliar (spec SP9D) — a
// diferencia de Anulada, que nunca llegó a regir y no tiene impacto. Valores
// estables.
public enum EstadoPublicacionPrecioHuevo
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
    Corregida = 3,
}
