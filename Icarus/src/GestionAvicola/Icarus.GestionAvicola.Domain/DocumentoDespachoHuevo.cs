using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Respaldo fotográfico de la nota de entrega que el trabajador sube al
// despachar (spec SP9): a diferencia de SP8D (donde el receptor sube la
// foto), aquí la sube el emisor en el mismo paso que envía. Un solo
// documento por despacho, inmutable tras crearse.
public sealed class DocumentoDespachoHuevo : Entity
{
    private DocumentoDespachoHuevo()
    {
    }

    internal DocumentoDespachoHuevo(
        Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes,
        long tamanoVistaBytes, string hashSha256, string nombreSeguro)
    {
        Id = Guid.Empty; // Ver DocumentoNotaEntrega (SP8D): EF lo registra Added por navegación.
        ClaveOriginal = claveOriginal;
        ClaveVista = claveVista;
        Mime = mime;
        TamanoBytes = tamanoBytes;
        TamanoVistaBytes = tamanoVistaBytes;
        HashSha256 = hashSha256;
        NombreSeguro = nombreSeguro;
        FechaUtc = DateTime.UtcNow;
    }

    public Guid ClaveOriginal { get; private set; }

    public Guid ClaveVista { get; private set; }

    public string Mime { get; private set; } = string.Empty;

    public long TamanoBytes { get; private set; }

    public long TamanoVistaBytes { get; private set; }

    public string HashSha256 { get; private set; } = string.Empty;

    public string NombreSeguro { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}
