namespace Icarus.GestionAvicola.Domain;

// Estados de una publicación de precio de huevo (spec SP9), mismo ciclo que
// EstadoNotificacionPreciosAlimentos: Borrador editable, Publicada inmutable
// y vigente hasta que otra publicación posterior entra en vigor, Anulada solo
// alcanza a publicaciones futuras. Valores estables.
public enum EstadoPublicacionPrecioHuevo
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
}
