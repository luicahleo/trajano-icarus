namespace Icarus.GestionAvicola.Application.Documentos;

// Almacenamiento privado de los respaldos de notas de entrega (spec SP8C
// "Documentos privados"): SQL conserva solo la clave lógica opaca y los
// metadatos; el volumen físico queda fuera del web root y forma parte del
// backup externo de la VPS. El contrato permite migrar a un almacenamiento
// S3 compatible sin tocar el dominio.
public interface IAlmacenDocumentosPedido
{
    // Valida firma/MIME/tamaño/dimensiones, rechaza polyglots, guarda el
    // original inmutable y genera la copia de visualización segura.
    Task<DocumentoAlmacenado> GuardarAsync(
        Stream contenido, CancellationToken cancellationToken = default);

    Task<Stream?> AbrirOriginalAsync(
        Guid clave, CancellationToken cancellationToken = default);

    Task<Stream?> AbrirVistaAsync(
        Guid clave, CancellationToken cancellationToken = default);
}

// Metadatos del documento guardado: lo único que llega a SQL, siempre por la
// relación con la nota (nunca rutas físicas, ni Base64, ni URL pública).
public sealed record DocumentoAlmacenado(
    Guid ClaveOriginal,
    Guid ClaveVista,
    string HashSha256,
    string Mime,
    long TamanoOriginalBytes,
    long TamanoVistaBytes);

// Límites de validación configurables sin cambiar código: tamaño máximo por
// archivo, dimensiones máximas de la imagen (en pixeles por lado) y cantidad
// máxima de imágenes activas por nota (páginas y reverso).
public sealed class OpcionesAlmacenDocumentosPedido
{
    public const string Seccion = "AlmacenDocumentosPedido";

    public string Ruta { get; set; } = string.Empty;

    public long MaxTamanoBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxDimensionesPixeles { get; set; } = 8000;

    public int MaxDocumentosPorNota { get; set; } = 8;
}
