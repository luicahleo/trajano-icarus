using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Respaldo probatorio de la nota en papel (spec SP8C "Documentos privados"):
// una imagen del original guardada en un volumen privado. SQL conserva solo
// la clave lógica opaca de original y vista, el MIME, los tamaños, el hash
// SHA-256 y un nombre seguro para mostrar; nunca la ruta física, Base64 ni
// una URL pública. El contenido lo custodia IAlmacenDocumentosPedido.
public sealed class DocumentoNotaEntrega : Entity
{
    private DocumentoNotaEntrega()
    {
    }

    internal DocumentoNotaEntrega(
        Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes,
        long tamanoVistaBytes, string hashSha256, string nombreSeguro)
    {
        ClaveOriginal = claveOriginal;
        ClaveVista = claveVista;
        Mime = mime;
        TamanoBytes = tamanoBytes;
        TamanoVistaBytes = tamanoVistaBytes;
        HashSha256 = hashSha256;
        NombreSeguro = nombreSeguro;
        FechaUtc = DateTime.UtcNow;
        Activo = true;
    }

    public Guid ClaveOriginal { get; private set; }

    public Guid ClaveVista { get; private set; }

    public string Mime { get; private set; } = string.Empty;

    public long TamanoBytes { get; private set; }

    public long TamanoVistaBytes { get; private set; }

    public string HashSha256 { get; private set; } = string.Empty;

    public string NombreSeguro { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }

    public bool Activo { get; private set; }

    // Trazabilidad de la sustitución (spec SP8C): el documento publicado es
    // inmutable; la corrección desactiva esta versión y referencia a la nueva.
    public Guid? ReemplazadoPorId { get; private set; }

    public DateTime? FechaDesactivacionUtc { get; private set; }

    internal void Desactivar(Guid reemplazadoPorId)
    {
        Activo = false;
        ReemplazadoPorId = reemplazadoPorId;
        FechaDesactivacionUtc = DateTime.UtcNow;
    }
}

// Datos que el almacenamiento privado entrega al dominio tras validar y
// guardar el archivo; la creación de claves y hash es responsabilidad del
// almacén, nunca del agregado.
public sealed record DatosDocumentoNota(
    Guid ClaveOriginal,
    Guid ClaveVista,
    string Mime,
    long TamanoBytes,
    long TamanoVistaBytes,
    string HashSha256,
    string NombreSeguro);
