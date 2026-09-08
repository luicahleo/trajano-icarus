using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Respaldo probatorio de la nota en papel que recibe el tenant al recibir la
// mercadería (spec SP8D): una imagen guardada en un volumen privado. SQL
// conserva solo la clave lógica opaca de original y vista, el MIME, los
// tamaños, el hash SHA-256 y un nombre seguro para mostrar; nunca la ruta
// física, Base64 ni una URL pública. El contenido lo custodia
// IAlmacenDocumentosPedido. Se crea una sola vez, junto con la confirmación
// de recepción: no admite sustitución posterior porque la recepción es
// terminal.
public sealed class DocumentoNotaEntrega : Entity
{
    private DocumentoNotaEntrega()
    {
    }

    internal DocumentoNotaEntrega(
        Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes,
        long tamanoVistaBytes, string hashSha256, string nombreSeguro)
    {
        // Nace con la clave vacía: EF la descubre por la navegación de la
        // entrega y la registra como Added (patrón AgregarDetalle). Sin
        // sustitución, no hace falta una clave estable antes de guardar.
        Id = Guid.Empty;
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
