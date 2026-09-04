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
}
