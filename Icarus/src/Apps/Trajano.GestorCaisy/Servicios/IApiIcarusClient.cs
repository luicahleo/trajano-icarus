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

    Task<Stream> DescargarDocumentoOriginalAsync(Guid id, CancellationToken token = default);

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

    // Despacho (SP8C): registra la entrega/nota con líneas manuales; después
    // cada respaldo de la nota se sube con su propio multipart. La vista
    // derivada se descarga inline para mostrarla en el detalle.
    Task DespacharPedidoAsync(ComandoDespachoApi comando, CancellationToken token = default);

    Task<Guid> SubirDocumentoNotaAsync(
        Guid id, Stream contenido, string nombreArchivo,
        Guid? reemplazaDocumentoId, CancellationToken token = default);

    Task<(Stream Contenido, string TipoContenido)> DescargarDocumentoNotaAsync(
        Guid id, Guid documentoId, CancellationToken token = default);
}
