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

// Pedidos de alimento (SP8B): espejo de los DTO de la API para la bandeja
// global del tenant-caisy con filtros y paginación.
public sealed record FiltrosPedidosApi(
    string? Estado, string? Presentacion, int Pagina, int TamanoPagina);

public sealed record PedidoResumenApi(
    Guid Id, Guid ClienteId, string Estado, string Presentacion, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado, int CantidadLineas);

public sealed record PaginaPedidosApi(
    IReadOnlyList<PedidoResumenApi> Items, int Total, int Pagina, int TamanoPagina);

public sealed record LineaPedidoApi(
    Guid Id, string TipoAlimento, string Presentacion, int CantidadSolicitada,
    int Equivalentes40Kg, decimal? PrecioFinalPor40Kg, decimal? SubtotalSolicitado,
    Guid? NotificacionPreciosAlimentosId);

public sealed record TransicionPedidoApi(
    string EstadoOrigen, string EstadoDestino, DateTime FechaUtc,
    string? Motivo, DateOnly? FechaEntregaEstimada);

public sealed record PedidoDetalleApi(
    Guid Id, Guid ClienteId, string Estado, DateOnly? FechaPedido,
    DateOnly? FechaEntregaEstimada, decimal? TotalSolicitado,
    IReadOnlyList<LineaPedidoApi> Lineas, IReadOnlyList<TransicionPedidoApi> Historial);

public sealed record NotificacionPedidoApi(
    Guid Id, string Tipo, Guid PedidoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record BandejaNotificacionesApi(
    IReadOnlyList<NotificacionPedidoApi> Items, int Contador);
