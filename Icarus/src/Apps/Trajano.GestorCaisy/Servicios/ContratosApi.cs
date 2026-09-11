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

// Publicaciones de precio de huevo (SP9A): espejo de los DTO del backend. El
// precio unitario (al productor + servicio) lo calcula la API; el tamaño viaja
// como nombre JSON (Extra, Primera, Segunda, Tercera, Cuarta, Quinta).
public sealed record PublicacionPrecioHuevoResumenApi(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record DetallePrecioHuevoApi(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario);

public sealed record PublicacionPrecioHuevoDetalleApi(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    decimal Servicio, Guid? DocumentoOriginalId, IReadOnlyList<DetallePrecioHuevoApi> Detalles);

public sealed record DatosDetalleHuevoApi(
    string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento = null);

public sealed record ComandoActualizarBorradorHuevoApi(
    Guid PublicacionId, DateOnly FechaNotificacion, DateOnly FechaVigencia,
    decimal Servicio, IReadOnlyList<DatosDetalleHuevoApi> Detalles);

public sealed record AjusteCorreccionHuevoResumenApi(Guid DespachoHuevoId, DateOnly? FechaRecepcion, decimal Monto);

public sealed record VistaPreviaCorreccionHuevoApi(
    IReadOnlyList<AjusteCorreccionHuevoResumenApi> Ajustes, decimal Total,
    // Tamaños de líneas congeladas con la errónea que la correctiva no cubre:
    // quedan con el precio erróneo sin compensación. Anulable para no romper
    // respuestas de una API que aún no lo envía.
    IReadOnlyList<string>? TamanosSinPrecioCorrectivo = null);

public sealed record ComandoCorregirVigenteHuevoApi(Guid PublicacionErroneaId, Guid PublicacionCorrectivaId, string Motivo);

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
    Guid Id, string NombreSeguro, string Mime, long TamanoBytes);

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

// Recepción de huevo (SP9C): espejo de los DTO del grupo
// /despachos-huevo-caisy para la bandeja del GestorCaisy. El tamaño viaja como
// nombre JSON (Extra, Primera, ...); el precio congelado es el snapshot del
// despacho y nunca se recalcula.
public sealed record FiltrosDespachosHuevoApi(
    string? Estado, int Pagina, int TamanoPagina);

public sealed record DespachoHuevoResumenApi(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras,
    int TotalHuevos, decimal? TotalBs);

public sealed record PaginaDespachosHuevoApi(
    IReadOnlyList<DespachoHuevoResumenApi> Items, int Total);

public sealed record DetalleDespachoHuevoApi(
    Guid Id, string Tamano, int CantidadAmarras, int UnidadesSueltas,
    int CantidadHuevos, decimal? PrecioUnitarioCongelado, decimal? Subtotal);

public sealed record DespachoHuevoDetalleApi(
    Guid Id, string Estado, DateOnly? FechaDespacho, int TotalAmarras,
    int TotalHuevos, decimal? TotalBs, IReadOnlyList<DetalleDespachoHuevoApi> Detalles);

// Las notificaciones de despacho de huevo usan una entidad dedicada (SP9): el
// identificador del despacho es anulable porque CreditoInsuficiente se origina
// en un pedido de alimento, no en un despacho.
public sealed record NotificacionDespachoHuevoApi(
    Guid Id, string Tipo, Guid? DespachoHuevoId, DateTime FechaUtc, bool Leida, string? Meta);

public sealed record BandejaNotificacionesDespachoHuevoApi(
    IReadOnlyList<NotificacionDespachoHuevoApi> Items, int Contador);

// Crédito por despachos de huevo del cliente de un pedido, para la decisión
// de CAISY (spec 2026-09-11-credito-huevo-vista-caisy-design). Espejo de
// CreditoHuevoDePedidoCaisy: el saldo ya tiene descontado el pedido cuando
// está comprometido, y por eso viajan también el monto que aporta y el saldo
// sin él. Cliente y CAISY ven exactamente la misma información, incluidos los
// ajustes con su motivo.
public sealed record AjusteCreditoHuevoApi(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

public sealed record CreditoHuevoPedidoApi(
    decimal SaldoDisponible, decimal MontoDelPedido, decimal SaldoSinEstePedido,
    bool PedidoComputadoEnElSaldo, IReadOnlyList<AjusteCreditoHuevoApi> Ajustes);
