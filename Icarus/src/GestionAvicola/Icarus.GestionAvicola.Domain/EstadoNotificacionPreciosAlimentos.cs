namespace Icarus.GestionAvicola.Domain;

// Estados de una Notificación de Precios de Alimentos (spec SP8). Borrador es
// editable; Publicada es inmutable y rige desde VigenteDesde hasta que entra
// en vigor otra publicación posterior; Anulada solo alcanza a publicaciones
// futuras. Valores estables porque se persisten como entero.
public enum EstadoNotificacionPreciosAlimentos
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
}
