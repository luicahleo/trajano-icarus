using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class PlantillaFacialProtegida : Entity
{
    private PlantillaFacialProtegida()
    {
    }

    public PlantillaFacialProtegida(
        Guid clienteId,
        Guid trabajadorId,
        byte[] contenidoCifrado,
        byte[] nonce,
        byte[] tag,
        int versionClave,
        string modeloFormato,
        int versionEnrolamiento)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La plantilla debe pertenecer a un cliente.");
        if (trabajadorId == Guid.Empty)
            throw new ReglaNegocioException("La plantilla debe pertenecer a un trabajador.");
        if (contenidoCifrado.Length == 0 || nonce.Length == 0 || tag.Length == 0)
            throw new ReglaNegocioException("El contenido cifrado está incompleto.");
        if (string.IsNullOrWhiteSpace(modeloFormato))
            throw new ReglaNegocioException("El modelo y formato de la plantilla son obligatorios.");
        if (versionEnrolamiento <= 0)
            throw new ReglaNegocioException("La versión de enrolamiento debe ser mayor a cero.");

        ClienteId = clienteId;
        TrabajadorId = trabajadorId;
        ContenidoCifrado = contenidoCifrado;
        Nonce = nonce;
        Tag = tag;
        VersionClave = versionClave;
        ModeloFormato = modeloFormato.Trim();
        VersionEnrolamiento = versionEnrolamiento;
        EstaActivo = true;
        FechaCreacionUtc = DateTimeOffset.UtcNow;
    }

    public Guid ClienteId { get; private set; }
    public Guid TrabajadorId { get; private set; }
    public byte[] ContenidoCifrado { get; private set; } = [];
    public byte[] Nonce { get; private set; } = [];
    public byte[] Tag { get; private set; } = [];
    public int VersionClave { get; private set; }
    public string ModeloFormato { get; private set; } = string.Empty;
    public int VersionEnrolamiento { get; private set; }
    public bool EstaActivo { get; private set; }
    public DateTimeOffset FechaCreacionUtc { get; private set; }
    public DateTimeOffset? FechaRevocacionUtc { get; private set; }

    public void Revocar()
    {
        EstaActivo = false;
        FechaRevocacionUtc = DateTimeOffset.UtcNow;
    }
}
