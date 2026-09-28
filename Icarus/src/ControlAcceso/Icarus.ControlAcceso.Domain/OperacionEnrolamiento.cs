using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Resultado local e idempotente de un intento de enrolamiento. No contiene
// imágenes, vectores ni datos biométricos: solo el desenlace y la versión.
public sealed class OperacionEnrolamiento : Entity
{
    private OperacionEnrolamiento()
    {
    }

    private OperacionEnrolamiento(
        Guid clienteId,
        Guid trabajadorId,
        Guid claveIdempotencia,
        EstadoOperacionEnrolamiento estado,
        int versionEnrolamiento,
        string? motivo,
        DateTimeOffset creadaEnUtc)
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
        Estado = estado;
        VersionEnrolamiento = versionEnrolamiento;
        Motivo = motivo;
        CreadaEnUtc = creadaEnUtc;
    }

    public Guid ClienteId { get; private set; }

    public Guid TrabajadorId { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public EstadoOperacionEnrolamiento Estado { get; private set; }

    public int VersionEnrolamiento { get; private set; }

    // Código genérico del fallo ("pad_fallido", "sin_rostro", ...).
    public string? Motivo { get; private set; }

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public static OperacionEnrolamiento Confirmada(
        Guid clienteId, Guid trabajadorId, Guid claveIdempotencia, int versionEnrolamiento,
        DateTimeOffset creadaEnUtc) =>
        new(clienteId, trabajadorId, claveIdempotencia,
            EstadoOperacionEnrolamiento.Confirmada, versionEnrolamiento, null, creadaEnUtc);

    public static OperacionEnrolamiento Fallida(
        Guid clienteId, Guid trabajadorId, Guid claveIdempotencia, string motivo,
        DateTimeOffset creadaEnUtc) =>
        new(clienteId, trabajadorId, claveIdempotencia,
            EstadoOperacionEnrolamiento.Fallida, 0, motivo, creadaEnUtc);
}
