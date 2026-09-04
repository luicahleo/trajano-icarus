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
    IReadOnlyList<LineaPedidoApi> Lineas, IReadOnlyList<TransicionPedidoApi> Historial,
    EntregaPedidoApi? Entrega = null, RecepcionPedidoApi? Recepcion = null);

public sealed record NotificacionPedidoApi(
    Guid Id, string Tipo, Guid PedidoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record BandejaNotificacionesApi(
    IReadOnlyList<NotificacionPedidoApi> Items, int Contador);

// Despacho y recepción (SP8C): espejo de la entrega/nota con sus respaldos y
// de la recepción con su snapshot de diferencias.
public sealed record LineaEntregaApi(
    string TipoAlimento, int CantidadEntregada, int Equivalentes40Kg);

public sealed record DocumentoNotaApi(
    Guid Id, string NombreSeguro, string Mime, long TamanoBytes, bool Activo);

public sealed record EntregaPedidoApi(
    string NumeroNota, DateOnly FechaNota, DateOnly FechaDespacho,
    decimal? TotalNetoInformado, decimal TotalDespachado,
    IReadOnlyList<LineaEntregaApi> Lineas,
    IReadOnlyList<DocumentoNotaApi> Documentos);

public sealed record LineaRecepcionApi(
    string TipoAlimento, int CantidadRecibida, int Equivalentes40Kg);

public sealed record DiferenciaRecepcionApi(
    string TipoAlimento, int CantidadRecibida, int CantidadEntregada, int Diferencia);

public sealed record RecepcionPedidoApi(
    DateOnly FechaRecepcion, decimal TotalRecibido,
    IReadOnlyList<LineaRecepcionApi> Lineas,
    IReadOnlyList<DiferenciaRecepcionApi> Diferencias);

public sealed record LineaDespachoApi(string TipoAlimento, int CantidadEntregada);

public sealed record ComandoDespachoApi(
    Guid Id, string NumeroNota, DateOnly FechaNota, decimal? TotalInformado,
    IReadOnlyList<LineaDespachoApi> Lineas);
