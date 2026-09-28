namespace Icarus.ControlAcceso.Domain;

public enum EstadoOperacionMarcacion
{
    // Salida propuesta tras elegir Entrada con una entrada abierta: espera la
    // segunda confirmación del trabajador y caduca.
    Reservada,
    Confirmada,
    Fallida
}
