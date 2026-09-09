using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.PreciosHuevo;

// Puertos de la publicación de precio de huevo (spec SP9). El catálogo es
// global: los repositorios explícitos de esta interfaz acceden sin filtro de
// tenant y la autorización vive en la política de CAISY. El documento original
// se almacena con el mismo IAlmacenDocumentosPrecios de PreciosAlimentos (el
// contrato ya es genérico: GuardarAsync(Stream) / AbrirAsync(Guid)).
public interface IRepositorioPublicacionesPreciosHuevo
{
    void Agregar(PublicacionPrecioHuevo publicacion);

    // Los detalles recreados por ActualizarBorrador llevan clave Guid
    // generada en el dominio: se registran como Added explícitamente (mismo
    // motivo que IRepositorioNotificacionesPrecios.AgregarDetalle).
    void AgregarDetalle(DetallePrecioHuevo detalle);

    Task<PublicacionPrecioHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    // Resolución de la vigente (spec SP9): última Publicada con
    // FechaVigencia <= fecha; no hace falta proceso programado.
    Task<PublicacionPrecioHuevo?> ObtenerVigenteAsync(
        DateOnly fecha, CancellationToken cancellationToken = default);

    // Dos publicaciones activas no pueden compartir la misma vigencia
    // (spec SP9); el índice filtrado de la base respalda esta comprobación.
    Task<bool> ExistePublicadaConVigenciaIgualAsync(
        DateOnly fechaVigencia, Guid? excluyendoId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicacionPrecioHuevo>> ListarHistorialAsync(
        CancellationToken cancellationToken = default);
}

// Importador determinista del Excel original (spec SP9): devuelve la propuesta
// o la lista de errores; nunca precios parciales. ClosedXML vive solo en
// Infrastructure, detrás de esta interfaz.
public interface IImportadorPublicacionPrecioHuevoExcel
{
    ResultadoImportacionPrecioHuevo Importar(Stream contenido);
}

public sealed record DatosPublicacionPrecioHuevo(
    DateOnly FechaNotificacion, DateOnly FechaVigencia, decimal Servicio,
    IReadOnlyList<DatosDetallePrecioHuevo> Detalles);

public sealed record ErrorImportacionPrecioHuevo(int? Fila, string Mensaje);

public sealed record ResultadoImportacionPrecioHuevo(
    DatosPublicacionPrecioHuevo? Propuesta, IReadOnlyList<ErrorImportacionPrecioHuevo> Errores);
