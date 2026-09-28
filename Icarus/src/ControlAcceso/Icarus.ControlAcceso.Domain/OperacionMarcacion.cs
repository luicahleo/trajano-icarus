using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Operación idempotente de marcación del kiosco. Reserva la propuesta de
// Salida y confirma el evento; no guarda muestras ni vectores.
public sealed class OperacionMarcacion : Entity
{
    private OperacionMarcacion()
    {
    }

    private OperacionMarcacion(
        Guid clienteId,
        Guid trabajadorId,
        Guid claveIdempotencia,
        TipoMarcacion accion,
        EstadoOperacionMarcacion estado,
        Guid? jornadaId,
        DateTimeOffset creadaEnUtc,
        DateTimeOffset? expiraEnUtc,
        string? motivo)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La operación debe pertenecer a un cliente.");
        if (trabajadorId == Guid.Empty)
            throw new ReglaNegocioException("La operación debe pertenecer a un trabajador.");
        if (claveIdempotencia == Guid.Empty)
            throw new ReglaNegocioException("La operación requiere una clave de idempotencia.");

        ClienteId = clienteId;
        TrabajadorId = trabajadorId;
        ClaveIdempotencia = claveIdempotencia;
        Accion = accion;
        Estado = estado;
        JornadaId = jornadaId;
        CreadaEnUtc = creadaEnUtc;
        ExpiraEnUtc = expiraEnUtc;
        Motivo = motivo;
    }

    public Guid ClienteId { get; private set; }

    public Guid TrabajadorId { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public TipoMarcacion Accion { get; private set; }

    public EstadoOperacionMarcacion Estado { get; private set; }

    public Guid? JornadaId { get; private set; }

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public DateTimeOffset? ExpiraEnUtc { get; private set; }

    public string? Motivo { get; private set; }

    public bool EstaVigente(DateTimeOffset ahoraUtc) =>
        Estado == EstadoOperacionMarcacion.Reservada &&
        (ExpiraEnUtc is null || ahoraUtc < ExpiraEnUtc);

    public static OperacionMarcacion ReservarPropuestaSalida(
        Guid clienteId, Guid trabajadorId, Guid claveIdempotencia,
        DateTimeOffset creadaEnUtc, DateTimeOffset expiraEnUtc) =>
        new(clienteId, trabajadorId, claveIdempotencia, TipoMarcacion.Salida,
            EstadoOperacionMarcacion.Reservada, null, creadaEnUtc, expiraEnUtc, null);

    public static OperacionMarcacion Confirmada(
        Guid clienteId, Guid trabajadorId, Guid claveIdempotencia,
        TipoMarcacion accion, Guid jornadaId, DateTimeOffset creadaEnUtc) =>
        new(clienteId, trabajadorId, claveIdempotencia, accion,
            EstadoOperacionMarcacion.Confirmada, jornadaId, creadaEnUtc, null, null);

    public static OperacionMarcacion Fallida(
        Guid clienteId, Guid trabajadorId, Guid claveIdempotencia,
        string motivo, DateTimeOffset creadaEnUtc) =>
        new(clienteId, trabajadorId, claveIdempotencia, TipoMarcacion.Entrada,
            EstadoOperacionMarcacion.Fallida, null, creadaEnUtc, null, motivo);

    public void Confirmar(Guid jornadaId) => JornadaId = jornadaId;
}
