namespace Icarus.GestionAvicola.Domain;

// Tipos de notificación interna del despacho de huevo (spec SP9). Valores
// estables: agregar al final, nunca renumerar.
public enum TipoNotificacionDespachoHuevo
{
    DespachoRecibido = 0,
    CreditoInsuficiente = 1,
}
