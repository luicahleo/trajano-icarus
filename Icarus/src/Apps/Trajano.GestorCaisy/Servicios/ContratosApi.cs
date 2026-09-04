using System.Text.Json.Serialization;

namespace Trajano.GestorCaisy.Servicios;

// Contrato tipado con la API de Trajano-Icarus (spec SP8). Los enums del
// contrato (tipo y presentación de alimento) viajan como nombres JSON, igual
// que en la PWA. Los record son espejo de los DTO del backend.

public sealed record SesionApi(string AccessToken, string? RefreshToken, int ExpiraEnSegundos);

public sealed record NotificacionPreciosResumenApi(
    Guid Id, DateOnly FechaDocumento, DateOnly VigenteDesde, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record DetallePrecioApi(
    Guid Id, string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    decimal? PrecioActualDocumento, int? EdadDesdeDias, int? EdadHastaDias);

public sealed record NotificacionPreciosDetalleApi(
    Guid Id, DateOnly FechaDocumento, DateOnly VigenteDesde, string Estado,
    decimal AporteCaisy, decimal Fondo, decimal Servicios, Guid? DocumentoOriginalId,
    IReadOnlyList<DetallePrecioApi> Detalles);

public sealed record DatosDetalleApi(
    string TipoAlimento, string Presentacion, decimal PrecioFinalPor40Kg,
    int? EdadDesdeDias, int? EdadHastaDias, decimal? PrecioActualDocumento = null);

public sealed record ComandoActualizarBorradorApi(
    Guid NotificacionId, DateOnly FechaDocumento, DateOnly VigenteDesde,
    decimal AporteCaisy, decimal Fondo, decimal Servicios,
    IReadOnlyList<DatosDetalleApi> Detalles);

// El importador responde con el identificador del borrador creado.
public sealed record BorradorImportadoApi([property: JsonPropertyName("id")] Guid Id);
