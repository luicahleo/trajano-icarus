using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Credencial restringida y revocable de un tenant. No representa un dispositivo
// ni un punto de acceso: solo habilita los endpoints del kiosco. La credencial
// se guarda hasheada; el token en claro solo viaja en la cookie host-only.
public sealed class SesionKiosco : Entity
{
    private SesionKiosco()
    {
    }

    public SesionKiosco(
        Guid clienteId,
        string hashCredencial,
        DateTimeOffset creadaEnUtc,
        DateTimeOffset expiraEnUtc)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La sesión de kiosco debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(hashCredencial))
            throw new ReglaNegocioException("La sesión de kiosco requiere una credencial.");
        if (expiraEnUtc <= creadaEnUtc)
            throw new ReglaNegocioException("La expiración debe ser posterior a la creación de la sesión.");

        ClienteId = clienteId;
        HashCredencial = hashCredencial.Trim();
        CreadaEnUtc = creadaEnUtc;
        ExpiraEnUtc = expiraEnUtc;
        EstaActiva = true;
    }

    public Guid ClienteId { get; private set; }

    public string HashCredencial { get; private set; } = string.Empty;

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public DateTimeOffset ExpiraEnUtc { get; private set; }

    public DateTimeOffset? RevocadaEnUtc { get; private set; }

    public bool EstaActiva { get; private set; }

    // Contador de concurrencia optimista: dos reemplazos simultáneos no pueden
    // dejar dos sesiones vigentes del mismo tenant.
    public int Version { get; private set; }

    public bool EstaVigente(DateTimeOffset ahoraUtc) => EstaActiva && ahoraUtc < ExpiraEnUtc;

    public void Revocar(DateTimeOffset ahoraUtc)
    {
        if (!EstaActiva)
            return;

        EstaActiva = false;
        RevocadaEnUtc = ahoraUtc;
        Version++;
    }
}
