namespace Trajano.GestorCaisy.Servicios;

// Cliente tipado de la API de Trajano-Icarus. La aplicación de oficina no
// tiene DbContext ni acceso SQL: toda operación pasa por estos métodos (spec SP8).
public interface IApiIcarusClient
{
    Task<SesionApi> IniciarSesionAsync(string correo, string contrasena, CancellationToken token = default);

    Task<IReadOnlyList<NotificacionPreciosResumenApi>> ListarNotificacionesAsync(CancellationToken token = default);

    Task<NotificacionPreciosDetalleApi> ObtenerNotificacionAsync(Guid id, CancellationToken token = default);

    Task<Guid> ImportarPdfAsync(Stream contenido, string nombreArchivo, CancellationToken token = default);

    Task ActualizarBorradorAsync(ComandoActualizarBorradorApi comando, CancellationToken token = default);

    Task PublicarAsync(Guid id, CancellationToken token = default);

    Task AnularFuturaAsync(Guid id, CancellationToken token = default);

    Task DescartarBorradorAsync(Guid id, CancellationToken token = default);

    Task<Stream> DescargarDocumentoOriginalAsync(Guid id, CancellationToken token = default);

    // Publicaciones de precio de huevo (SP9A): catálogo global en
    // /precios-huevo-caisy, reservado a GestorRecepcionHuevos. El original se
    // importa como Excel (.xlsx), no como PDF.
    Task<IReadOnlyList<PublicacionPrecioHuevoResumenApi>> ListarPublicacionesHuevoAsync(
        CancellationToken token = default);

    Task<PublicacionPrecioHuevoDetalleApi> ObtenerPublicacionHuevoAsync(
        Guid id, CancellationToken token = default);

    Task<Guid> ImportarExcelHuevoAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default);

    Task ActualizarBorradorHuevoAsync(
        ComandoActualizarBorradorHuevoApi comando, CancellationToken token = default);

    Task PublicarHuevoAsync(Guid id, CancellationToken token = default);

    Task AnularFuturaHuevoAsync(Guid id, CancellationToken token = default);

    Task DescartarBorradorHuevoAsync(Guid id, CancellationToken token = default);

    Task<Stream> DescargarDocumentoOriginalHuevoAsync(Guid id, CancellationToken token = default);

    Task<PaginaPedidosApi> ListarPedidosAsync(
        FiltrosPedidosApi filtros, CancellationToken token = default);

    Task<PedidoDetalleApi> ObtenerPedidoAsync(Guid id, CancellationToken token = default);

    Task DevolverPedidoAsync(Guid id, string motivo, CancellationToken token = default);

    Task RechazarPedidoAsync(Guid id, string motivo, CancellationToken token = default);

    Task AceptarPedidoAsync(Guid id, DateOnly fechaEntregaEstimada, CancellationToken token = default);

    Task ActualizarEntregaEstimadaAsync(
        Guid id, DateOnly nuevaFecha, CancellationToken token = default);

    Task<BandejaNotificacionesApi> ListarNotificacionesPedidoAsync(CancellationToken token = default);

    Task MarcarNotificacionPedidoLeidaAsync(Guid id, CancellationToken token = default);

    // Despacho (SP8C): registra la entrega/nota con líneas manuales. La foto
    // de la nota ya no la sube CAISY: la adjunta el receptor al confirmar la
    // recepción (SP8D). La vista derivada del respaldo se descarga inline para
    // mostrarla en el detalle.
    Task DespacharPedidoAsync(ComandoDespachoApi comando, CancellationToken token = default);

    Task<(Stream Contenido, string TipoContenido)> DescargarDocumentoNotaAsync(
        Guid id, Guid documentoId, CancellationToken token = default);

    // Recibo imprimible (spec SP8D): PDF con los datos ya guardados del
    // despacho, para que CAISY lo firme/selle en papel.
    Task<Stream> ObtenerReciboPdfAsync(Guid id, CancellationToken token = default);

    // Recepción de huevo (SP9C): bandeja global de despachos de huevo del
    // grupo /despachos-huevo-caisy. CAISY solo confirma la recepción y sirve
    // el recibo PDF; no hay negociación ni carga de archivos.
    Task<PaginaDespachosHuevoApi> ListarDespachosHuevoAsync(
        FiltrosDespachosHuevoApi filtros, CancellationToken token = default);

    Task<DespachoHuevoDetalleApi> ObtenerDespachoHuevoAsync(
        Guid id, CancellationToken token = default);

    Task ConfirmarRecepcionDespachoHuevoAsync(Guid id, CancellationToken token = default);

    Task<Stream> ObtenerReciboDespachoHuevoPdfAsync(Guid id, CancellationToken token = default);

    Task<BandejaNotificacionesDespachoHuevoApi> ListarNotificacionesDespachoHuevoAsync(
        CancellationToken token = default);

    Task MarcarNotificacionDespachoHuevoLeidaAsync(Guid id, CancellationToken token = default);
}
