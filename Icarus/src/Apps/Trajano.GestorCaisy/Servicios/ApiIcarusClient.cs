using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trajano.GestorCaisy.Observabilidad;

namespace Trajano.GestorCaisy.Servicios;

public sealed class ApiIcarusClient : IApiIcarusClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string CookieRefresh = "icarus_refresh";

    // Plantillas técnicas de observabilidad: las constantes las aporta el
    // código de cada operación; los valores dinámicos solo van a la URI real.
    private static class Plantillas
    {
        public const string Sesion = "/api/identidad/sesion";
        public const string Renovar = "/api/identidad/sesion/renovar";
        public const string Notificaciones = "/api/precios-alimentos/";
        public const string Notificacion = "/api/precios-alimentos/{id}";
        public const string NotificacionPublicar = "/api/precios-alimentos/{id}/publicar";
        public const string NotificacionAnular = "/api/precios-alimentos/{id}/anular";
        public const string NotificacionDocumento = "/api/precios-alimentos/{id}/documento-original";
        public const string NotificacionImportar = "/api/precios-alimentos/importar";
        public const string PublicacionesHuevo = "/api/precios-huevo-caisy/";
        public const string PublicacionHuevo = "/api/precios-huevo-caisy/{id}";
        public const string PublicacionHuevoPublicar = "/api/precios-huevo-caisy/{id}/publicar";
        public const string PublicacionHuevoAnular = "/api/precios-huevo-caisy/{id}/anular";
        public const string PublicacionHuevoDocumento = "/api/precios-huevo-caisy/{id}/documento-original";
        public const string PublicacionHuevoVigente = "/api/precios-huevo-caisy/vigente";
        public const string PublicacionHuevoImportar = "/api/precios-huevo-caisy/importar";
        public const string PrevisualizarCorreccion = "/api/precios-huevo-caisy/corregir/previsualizar";
        public const string CorregirVigente = "/api/precios-huevo-caisy/corregir";
        public const string Pedidos = "/api/pedidos-alimento-caisy";
        public const string Pedido = "/api/pedidos-alimento-caisy/{id}";
        public const string PedidoCredito = "/api/pedidos-alimento-caisy/{id}/credito";
        public const string PedidoDevolver = "/api/pedidos-alimento-caisy/{id}/devolver";
        public const string PedidoRechazar = "/api/pedidos-alimento-caisy/{id}/rechazar";
        public const string PedidoAceptar = "/api/pedidos-alimento-caisy/{id}/aceptar";
        public const string PedidoEntregaEstimada = "/api/pedidos-alimento-caisy/{id}/entrega-estimada";
        public const string PedidosNotificaciones = "/api/pedidos-alimento-caisy/notificaciones";
        public const string PedidoNotificacionLeida = "/api/pedidos-alimento-caisy/notificaciones/{id}/marcar-leida";
        public const string PedidoDespachar = "/api/pedidos-alimento-caisy/{id}/despachar";
        public const string PedidoDocumentoNota = "/api/pedidos-alimento-caisy/{id}/nota/documentos/{documentoId}/vista";
        public const string PedidoRecibo = "/api/pedidos-alimento-caisy/{id}/recibo.pdf";
        public const string DespachosHuevo = "/api/despachos-huevo-caisy";
        public const string DespachoHuevo = "/api/despachos-huevo-caisy/{id}";
        public const string DespachoHuevoConfirmar = "/api/despachos-huevo-caisy/{id}/confirmar-recepcion";
        public const string DespachoHuevoRecibo = "/api/despachos-huevo-caisy/{id}/recibo.pdf";
        public const string DespachosHuevoNotificaciones = "/api/despachos-huevo-caisy/notificaciones";
        public const string DespachoHuevoNotificacionLeida = "/api/despachos-huevo-caisy/notificaciones/{id}/marcar-leida";
    }

    private readonly HttpClient _http;
    private readonly string? _baseUrl;
    private readonly IHttpContextAccessor _contexto;
    private readonly ISesionCaisyActual _sesion;
    private readonly ILogger<ApiIcarusClient> _registro;

    public ApiIcarusClient(
        HttpClient http, IConfiguration configuracion,
        IHttpContextAccessor contexto, ISesionCaisyActual sesion,
        ILogger<ApiIcarusClient> registro)
    {
        _http = http;
        _baseUrl = configuracion["ApiIcarus:BaseUrl"];
        _contexto = contexto;
        _sesion = sesion;
        _registro = registro;
    }

    public async Task<SesionApi> IniciarSesionAsync(
        string correo, string contrasena, CancellationToken token = default)
    {
        // Renovable en falso: renovar una renovación solo multiplicaría los
        // intentos con credenciales ya rechazadas.
        using var respuesta = await EnviarAsync(
            _ => PeticionJson(HttpMethod.Post, "identidad/sesion", Plantillas.Sesion, null,
                new { email = correo, contrasena }),
            renovable: false, token);
        await AsegurarExitoAsync(respuesta, token);
        var sesion = await respuesta.Content
            .ReadFromJsonAsync<SesionApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta de sesión ilegible");
        var refresh = LeerCookieRefresh(respuesta)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "La sesión no trajo renovación");
        return sesion with { RefreshToken = refresh };
    }

    public async Task<IReadOnlyList<NotificacionPreciosResumenApi>> ListarNotificacionesAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, "precios-alimentos/",
                Plantillas.Notificaciones, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content
                .ReadFromJsonAsync<IReadOnlyList<NotificacionPreciosResumenApi>>(Json, token)
            ?? [];
    }

    public async Task<NotificacionPreciosDetalleApi> ObtenerNotificacionAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, $"precios-alimentos/{id}",
                Plantillas.Notificacion, accessToken),
            token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<NotificacionPreciosDetalleApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<Guid> ImportarPdfAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default)
    {
        // El PDF se copia a memoria (tope de 20 MB del lado de la API) para
        // que la renovación de sesión pueda reenviar la misma carga.
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, token);
        var bytes = memoria.ToArray();
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionMultipart(bytes, nombreArchivo, "precios-alimentos/importar",
                Plantillas.NotificacionImportar, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var borrador = await respuesta.Content.ReadFromJsonAsync<BorradorImportadoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta de importación ilegible");
        return borrador.Id;
    }

    public async Task ActualizarBorradorAsync(
        ComandoActualizarBorradorApi comando, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Put,
                $"precios-alimentos/{comando.NotificacionId}", Plantillas.Notificacion,
                accessToken, comando), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task PublicarAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-alimentos/{id}/publicar",
                Plantillas.NotificacionPublicar, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task AnularFuturaAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-alimentos/{id}/anular",
                Plantillas.NotificacionAnular, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task DescartarBorradorAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Delete, $"precios-alimentos/{id}",
                Plantillas.Notificacion, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<Stream> DescargarDocumentoOriginalAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, $"precios-alimentos/{id}/documento-original",
                Plantillas.NotificacionDocumento, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }

    public async Task<IReadOnlyList<PublicacionPrecioHuevoResumenApi>> ListarPublicacionesHuevoAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, "precios-huevo-caisy/",
                Plantillas.PublicacionesHuevo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content
                .ReadFromJsonAsync<IReadOnlyList<PublicacionPrecioHuevoResumenApi>>(Json, token)
            ?? [];
    }

    public async Task<PublicacionPrecioHuevoDetalleApi> ObtenerPublicacionHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, $"precios-huevo-caisy/{id}",
                Plantillas.PublicacionHuevo, accessToken),
            token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PublicacionPrecioHuevoDetalleApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<Guid> ImportarExcelHuevoAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default)
    {
        // El Excel se copia a memoria (tope de 5 MB del lado de la API) para
        // que la renovación de sesión pueda reenviar la misma carga.
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, token);
        var bytes = memoria.ToArray();
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionMultipart(bytes, nombreArchivo, "precios-huevo-caisy/importar",
                Plantillas.PublicacionHuevoImportar, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var borrador = await respuesta.Content.ReadFromJsonAsync<BorradorImportadoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta de importación ilegible");
        return borrador.Id;
    }

    public async Task ActualizarBorradorHuevoAsync(
        ComandoActualizarBorradorHuevoApi comando, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Put,
                $"precios-huevo-caisy/{comando.PublicacionId}", Plantillas.PublicacionHuevo,
                accessToken, comando), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task PublicarHuevoAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-huevo-caisy/{id}/publicar",
                Plantillas.PublicacionHuevoPublicar, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task AnularFuturaHuevoAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Post, $"precios-huevo-caisy/{id}/anular",
                Plantillas.PublicacionHuevoAnular, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task DescartarBorradorHuevoAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Delete, $"precios-huevo-caisy/{id}",
                Plantillas.PublicacionHuevo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<Stream> DescargarDocumentoOriginalHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, $"precios-huevo-caisy/{id}/documento-original",
                Plantillas.PublicacionHuevoDocumento, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }

    public async Task<PublicacionPrecioHuevoDetalleApi?> ObtenerPublicacionVigenteHuevoAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, "precios-huevo-caisy/vigente",
                Plantillas.PublicacionHuevoVigente, accessToken), token);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PublicacionPrecioHuevoDetalleApi>(Json, token);
    }

    public async Task<VistaPreviaCorreccionHuevoApi> PrevisualizarCorreccionHuevoAsync(
        Guid erroneaId, Guid correctivaId, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"precios-huevo-caisy/corregir/previsualizar?erronea={erroneaId}&correctiva={correctivaId}",
                Plantillas.PrevisualizarCorreccion, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<VistaPreviaCorreccionHuevoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task CorregirVigenteHuevoAsync(
        Guid erroneaId, Guid correctivaId, string motivo, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post, "precios-huevo-caisy/corregir",
                Plantillas.CorregirVigente, accessToken,
                new ComandoCorregirVigenteHuevoApi(erroneaId, correctivaId, motivo)), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<PaginaPedidosApi> ListarPedidosAsync(
        FiltrosPedidosApi filtros, CancellationToken token = default)
    {
        var consulta = new StringBuilder("pedidos-alimento-caisy?").AppendFormat(
            CultureInfo.InvariantCulture, "pagina={0}&tamanoPagina={1}", filtros.Pagina, filtros.TamanoPagina);
        if (!string.IsNullOrEmpty(filtros.Estado))
            consulta.Append("&estado=").Append(Uri.EscapeDataString(filtros.Estado));
        if (!string.IsNullOrEmpty(filtros.Presentacion))
            consulta.Append("&presentacion=").Append(Uri.EscapeDataString(filtros.Presentacion));
        if (!string.IsNullOrWhiteSpace(filtros.Granja))
            consulta.Append("&granja=").Append(Uri.EscapeDataString(filtros.Granja.Trim()));
        if (filtros.Desde is { } desdePedidos)
            consulta.Append("&desde=").Append(desdePedidos.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (filtros.Hasta is { } hastaPedidos)
            consulta.Append("&hasta=").Append(hastaPedidos.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (filtros.Numero is { } numeroPedido)
            consulta.Append("&numero=").Append(numeroPedido.ToString(CultureInfo.InvariantCulture));
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, consulta.ToString(),
                Plantillas.Pedidos, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PaginaPedidosApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<PedidoDetalleApi> ObtenerPedidoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, $"pedidos-alimento-caisy/{id}",
                Plantillas.Pedido, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PedidoDetalleApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<CreditoHuevoPedidoApi> ObtenerCreditoDePedidoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, $"pedidos-alimento-caisy/{id}/credito",
                Plantillas.PedidoCredito, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<CreditoHuevoPedidoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public Task DevolverPedidoAsync(
        Guid id, string motivo, CancellationToken token = default) =>
        EnviarDecisionPedidoAsync(id, "devolver", Plantillas.PedidoDevolver, motivo, token);

    public Task RechazarPedidoAsync(
        Guid id, string motivo, CancellationToken token = default) =>
        EnviarDecisionPedidoAsync(id, "rechazar", Plantillas.PedidoRechazar, motivo, token);

    private async Task EnviarDecisionPedidoAsync(
        Guid id, string accion, string plantilla, string motivo, CancellationToken token)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"pedidos-alimento-caisy/{id}/{accion}", plantilla, accessToken,
                new { motivo }), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task AceptarPedidoAsync(
        Guid id, DateOnly fechaEntregaEstimada, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"pedidos-alimento-caisy/{id}/aceptar", Plantillas.PedidoAceptar, accessToken,
                new { fechaEntregaEstimada }), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task ActualizarEntregaEstimadaAsync(
        Guid id, DateOnly nuevaFecha, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"pedidos-alimento-caisy/{id}/entrega-estimada", Plantillas.PedidoEntregaEstimada,
                accessToken, new { fechaEntregaEstimada = nuevaFecha }), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<BandejaNotificacionesApi> ListarNotificacionesPedidoAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, "pedidos-alimento-caisy/notificaciones",
                Plantillas.PedidosNotificaciones, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<BandejaNotificacionesApi>(Json, token)
            ?? new BandejaNotificacionesApi([], 0);
    }

    public async Task MarcarNotificacionPedidoLeidaAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"pedidos-alimento-caisy/notificaciones/{id}/marcar-leida",
                Plantillas.PedidoNotificacionLeida, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task DespacharPedidoAsync(
        ComandoDespachoApi comando, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"pedidos-alimento-caisy/{comando.Id}/despachar", Plantillas.PedidoDespachar,
                accessToken,
                new
                {
                    numeroNota = comando.NumeroNota,
                    fechaNota = comando.FechaNota,
                    totalInformado = comando.TotalInformado,
                    lineas = comando.Lineas.Select(l => new
                    {
                        tipoAlimento = l.TipoAlimento,
                        cantidadEntregada = l.CantidadEntregada,
                    }),
                }), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<(Stream Contenido, string TipoContenido)> DescargarDocumentoNotaAsync(
        Guid id, Guid documentoId, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"pedidos-alimento-caisy/{id}/nota/documentos/{documentoId}/vista",
                Plantillas.PedidoDocumentoNota, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return (memoria, tipo);
    }

    public async Task<Stream> ObtenerReciboPdfAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"pedidos-alimento-caisy/{id}/recibo.pdf", Plantillas.PedidoRecibo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }

    // SP9C: bandeja de recepción de huevo. Mismo cuerpo que los pares de
    // pedidos, con la ruta base /despachos-huevo-caisy.
    public async Task<PaginaDespachosHuevoApi> ListarDespachosHuevoAsync(
        FiltrosDespachosHuevoApi filtros, CancellationToken token = default)
    {
        var consulta = new StringBuilder("despachos-huevo-caisy?").AppendFormat(
            CultureInfo.InvariantCulture, "pagina={0}&tamanoPagina={1}", filtros.Pagina, filtros.TamanoPagina);
        if (!string.IsNullOrEmpty(filtros.Estado))
            consulta.Append("&estado=").Append(Uri.EscapeDataString(filtros.Estado));
        if (!string.IsNullOrWhiteSpace(filtros.Granja))
            consulta.Append("&granja=").Append(Uri.EscapeDataString(filtros.Granja.Trim()));
        if (filtros.Desde is { } desdeHuevo)
            consulta.Append("&desde=").Append(desdeHuevo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (filtros.Hasta is { } hastaHuevo)
            consulta.Append("&hasta=").Append(hastaHuevo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (filtros.Numero is { } numeroHuevo)
            consulta.Append("&numero=").Append(numeroHuevo.ToString(CultureInfo.InvariantCulture));
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, consulta.ToString(),
                Plantillas.DespachosHuevo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<PaginaDespachosHuevoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task<DespachoHuevoDetalleApi> ObtenerDespachoHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get, $"despachos-huevo-caisy/{id}",
                Plantillas.DespachoHuevo, accessToken),
            token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<DespachoHuevoDetalleApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }

    public async Task ConfirmarRecepcionDespachoHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"despachos-huevo-caisy/{id}/confirmar-recepcion",
                Plantillas.DespachoHuevoConfirmar, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    public async Task<Stream> ObtenerReciboDespachoHuevoPdfAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"despachos-huevo-caisy/{id}/recibo.pdf",
                Plantillas.DespachoHuevoRecibo, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }

    public async Task<BandejaNotificacionesDespachoHuevoApi> ListarNotificacionesDespachoHuevoAsync(
        CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, "despachos-huevo-caisy/notificaciones",
                Plantillas.DespachosHuevoNotificaciones, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<BandejaNotificacionesDespachoHuevoApi>(Json, token)
            ?? new BandejaNotificacionesDespachoHuevoApi([], 0);
    }

    public async Task MarcarNotificacionDespachoHuevoLeidaAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Post,
                $"despachos-huevo-caisy/notificaciones/{id}/marcar-leida",
                Plantillas.DespachoHuevoNotificacionLeida, accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
    }

    // Núcleo: envía con el access token actual; ante un 401 renueva la sesión
    // una vez y reintenta con el token fresco. Rutas de sesión nunca se
    // renuevan (evita bucles con credenciales inválidas).
    private async Task<HttpResponseMessage> EnviarConSesionAsync(
        Func<string?, HttpRequestMessage> crearPeticion, CancellationToken token) =>
        await EnviarAsync(crearPeticion, renovable: true, token);

    private async Task<HttpResponseMessage> EnviarAsync(
        Func<string?, HttpRequestMessage> crearPeticion, bool renovable, CancellationToken token)
    {
        var respuesta = await _http.SendAsync(crearPeticion(_sesion.AccessToken), token);
        if (!renovable
            || respuesta.StatusCode != HttpStatusCode.Unauthorized
            || string.IsNullOrEmpty(_sesion.RefreshToken))
            return respuesta;
        if (!await RenovarSesionAsync(token))
        {
            _registro.LogWarning("La renovación de sesión no prosperó; se informa el 401 original.");
            return respuesta;
        }
        respuesta.Dispose();
        return await _http.SendAsync(crearPeticion(_sesion.AccessToken), token);
    }

    private async Task<bool> RenovarSesionAsync(CancellationToken token)
    {
        var refresh = _sesion.RefreshToken;
        if (string.IsNullOrEmpty(refresh))
            return false;
        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, new Uri(ResolverBase() + "identidad/sesion/renovar"));
        peticion.EstablecerRuta(Plantillas.Renovar);
        peticion.Headers.TryAddWithoutValidation("Cookie", $"{CookieRefresh}={refresh}");
        using var respuesta = await _http.SendAsync(peticion, token);
        if (respuesta.StatusCode != HttpStatusCode.OK)
            return false;
        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionApi>(Json, token);
        var nuevoRefresh = LeerCookieRefresh(respuesta);
        if (sesion is null || string.IsNullOrEmpty(sesion.AccessToken) || nuevoRefresh is null)
            return false;
        await _sesion.RenovarTokensAsync(sesion.AccessToken, nuevoRefresh, token);
        return true;
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken token)
    {
        if (respuesta.IsSuccessStatusCode)
            return;
        string? titulo = null;
        string? correlacion = null;
        Dictionary<string, IReadOnlyList<string>>? errores = null;
        try
        {
            using var documento = JsonDocument.Parse(
                await respuesta.Content.ReadAsStringAsync(token));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("title", out var tituloJson))
                    titulo = tituloJson.GetString();
                if (raiz.TryGetProperty("correlationId", out var correlacionJson))
                    correlacion = correlacionJson.GetString();
                if (raiz.TryGetProperty("errors", out var erroresJson)
                    && erroresJson.ValueKind == JsonValueKind.Object)
                    errores = erroresJson.EnumerateObject().ToDictionary(
                        propiedad => propiedad.Name,
                        propiedad => (IReadOnlyList<string>)propiedad.Value
                            .EnumerateArray()
                            .Select(valor => valor.GetString() ?? string.Empty)
                            .ToArray());
            }
        }
        catch (JsonException)
        {
            // Cuerpos sin JSON (por ejemplo 401 o 403 vacíos): el estado basta.
        }
        throw new ErrorApiException(
            (int)respuesta.StatusCode, titulo, correlacion, errores);
    }

    private HttpRequestMessage PeticionJson(
        HttpMethod metodo, string ruta, string plantilla, string? accessToken, object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, new Uri(ResolverBase() + ruta));
        peticion.EstablecerRuta(plantilla);
        if (accessToken is not null)
            peticion.Headers.Authorization = new("Bearer", accessToken);
        if (cuerpo is not null)
            peticion.Content = new StringContent(
                JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json");
        return peticion;
    }

    private HttpRequestMessage PeticionMultipart(
        byte[] bytes, string nombreArchivo, string ruta, string plantilla, string? accessToken)
    {
        var peticion = new HttpRequestMessage(
            HttpMethod.Post, new Uri(ResolverBase() + ruta));
        peticion.EstablecerRuta(plantilla);
        if (accessToken is not null)
            peticion.Headers.Authorization = new("Bearer", accessToken);
        var parte = new StreamContent(new MemoryStream(bytes));
        parte.Headers.ContentType = new(ObtenerTipoContenidoArchivo(nombreArchivo));
        var cuerpo = new MultipartFormDataContent { { parte, "archivo", nombreArchivo } };
        peticion.Content = cuerpo;
        return peticion;
    }

    private static string ObtenerTipoContenidoArchivo(string nombreArchivo) =>
        string.Equals(Path.GetExtension(nombreArchivo), ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf";

    // La API vive bajo /api del mismo origen lógico en despliegue; si la
    // configuración trae una URL absoluta (desarrollo, pruebas) se usa tal cual.
    private string ResolverBase()
    {
        var base_ = _baseUrl ?? "/api";
        if (base_.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return base_.EndsWith('/') ? base_ : $"{base_}/";
        var contexto = _contexto.HttpContext
            ?? throw new InvalidOperationException(
                "La URL de la API es relativa y no hay petición en curso para resolverla.");
        var host = $"{contexto.Request.Scheme}://{contexto.Request.Host}";
        if (!base_.StartsWith('/'))
            return $"{host}/{base_}/";
        return base_.EndsWith('/') ? $"{host}{base_}" : $"{host}{base_}/";
    }

    private static string? LeerCookieRefresh(HttpResponseMessage respuesta)
    {
        if (!respuesta.Headers.TryGetValues("Set-Cookie", out var valores))
            return null;
        var cookie = valores.FirstOrDefault(valor =>
            valor.StartsWith($"{CookieRefresh}=", StringComparison.Ordinal));
        if (cookie is null)
            return null;
        var cuerpo = cookie[$"{CookieRefresh}=".Length..];
        var fin = cuerpo.IndexOf(';');
        return (fin < 0 ? cuerpo : cuerpo[..fin]).Trim();
    }
}
