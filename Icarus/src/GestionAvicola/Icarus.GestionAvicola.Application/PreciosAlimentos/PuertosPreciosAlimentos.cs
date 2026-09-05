using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.PreciosAlimentos;

// Puertos de la Notificación de Precios de Alimentos (spec SP8). El catálogo
// es global: los repositorios explícitos de esta interfaz acceden sin filtro
// de tenant y la autorización vive en la política de CAISY, no en
// IgnoreQueryFilters dispersos.
public interface IRepositorioNotificacionesPrecios
{
    void Agregar(NotificacionPreciosAlimentos notificacion);

    // Los detalles recreados por ActualizarBorrador llevan clave Guid generada
    // en el dominio: se registran como Added explícitamente (el DetectChanges
    // de EF Core los marcaría Modified por asumir que ya existen).
    void AgregarDetalle(DetallePrecioAlimento detalle);

    Task<NotificacionPreciosAlimentos?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    // Resolución de la vigente (spec SP8): última Publicada con
    // VigenteDesde <= fecha; no hace falta proceso programado.
    Task<NotificacionPreciosAlimentos?> ObtenerVigenteAsync(
        DateOnly fecha, CancellationToken cancellationToken = default);

    // Dos publicaciones activas no pueden compartir la misma vigencia
    // (spec SP8); el índice filtrado de la base respalda esta comprobación.
    Task<bool> ExistePublicadaConVigenciaIgualAsync(
        DateOnly vigenteDesde, Guid? excluyendoId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificacionPreciosAlimentos>> ListarHistorialAsync(
        CancellationToken cancellationToken = default);
}

// Importador determinista del PDF original (spec SP8): devuelve la propuesta
// o la lista de errores; nunca precios parciales. PdfPig vive solo en
// Infrastructure, detrás de esta interfaz.
public interface IImportadorNotificacionPreciosPdf
{
    ResultadoImportacionPdf Importar(Stream contenido);
}

public sealed record DatosNotificacionPdf(
    DateOnly FechaDocumento, DateOnly VigenteDesde,
    decimal AporteCaisy, decimal Fondo, decimal Servicios,
    IReadOnlyList<DatosDetallePrecio> Detalles);

public sealed record ErrorImportacionPdf(int? Fila, string Mensaje);

public sealed record ResultadoImportacionPdf(
    DatosNotificacionPdf? Propuesta, IReadOnlyList<ErrorImportacionPdf> Errores);

// Almacenamiento privado del PDF original (spec SP8): SQL solo conserva la
// clave lógica opaca; el volumen físico forma parte del backup externo.
public interface IAlmacenDocumentosPrecios
{
    Task<Guid> GuardarAsync(Stream contenido, CancellationToken cancellationToken = default);

    Task<Stream?> AbrirAsync(Guid clave, CancellationToken cancellationToken = default);
}
