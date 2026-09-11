using System.Globalization;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Tests.Ayudas;

// Doble de la API de Trajano-Icarus para las pruebas de flujo: scripta
// respuestas por operación y cuenta cuántas veces se invocó cada una.
public sealed class ApiIcarusFalsa : IApiIcarusClient
{
    public Func<string, string, SesionApi>? AlIniciarSesion { get; set; }
    public Exception? ErrorDeListar { get; set; }
    public Exception? ErrorDeObtener { get; set; }
    public Exception? ErrorDePublicar { get; set; }
    public Exception? ErrorDeAnular { get; set; }
    public Exception? ErrorDeDescartar { get; set; }
    public Exception? ErrorDeActualizar { get; set; }
    public Exception? ErrorDeImportar { get; set; }
    public Exception? ErrorDeDescargar { get; set; }

    public List<NotificacionPreciosResumenApi> Resumenes { get; } = [];
    public NotificacionPreciosDetalleApi? DetalleActual { get; set; }
    public Guid IdDeImportacion { get; set; } = Guid.NewGuid();
    public byte[] ContenidoPdf { get; set; } = "%PDF-1.7 prueba"u8.ToArray();
    public string NombreDeDescarga { get; set; } = "notificacion-precios-2025-11-02.pdf";

    public int IniciosDeSesion { get; private set; }
    public int VecesListar { get; private set; }
    public int VecesObtener { get; private set; }
    public int VecesImportar { get; private set; }
    public int VecesActualizar { get; private set; }
    public int VecesPublicar { get; private set; }
    public int VecesAnular { get; private set; }
    public int VecesDescartar { get; private set; }
    public int VecesDescargar { get; private set; }

    public Guid? UltimoObtenido { get; private set; }
    public Guid? UltimoPublicado { get; private set; }
    public Guid? UltimoAnulado { get; private set; }
    public Guid? UltimoDescartado { get; private set; }
    public (string Correo, string Contrasena)? UltimoAcceso { get; private set; }
    public ComandoActualizarBorradorApi? UltimoComando { get; private set; }
    public byte[]? UltimoPdfImportado { get; private set; }

    public Task<SesionApi> IniciarSesionAsync(
        string correo, string contrasena, CancellationToken token = default)
    {
        IniciosDeSesion++;
        UltimoAcceso = (correo, contrasena);
        return Task.FromResult(AlIniciarSesion is null
            ? new SesionApi(CreadorTokens.Crear(), CreadorTokens.Crear(), 900)
            : AlIniciarSesion(correo, contrasena));
    }

    public Task<IReadOnlyList<NotificacionPreciosResumenApi>> ListarNotificacionesAsync(
        CancellationToken token = default)
    {
        VecesListar++;
        if (ErrorDeListar is not null) throw ErrorDeListar;
        return Task.FromResult<IReadOnlyList<NotificacionPreciosResumenApi>>(Resumenes);
    }

    public Task<NotificacionPreciosDetalleApi> ObtenerNotificacionAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtener++;
        UltimoObtenido = id;
        if (ErrorDeObtener is not null) throw ErrorDeObtener;
        return Task.FromResult(DetalleActual ?? CrearDetalle(id, "Borrador"));
    }

    public Task<Guid> ImportarPdfAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default)
    {
        VecesImportar++;
        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        UltimoPdfImportado = memoria.ToArray();
        if (ErrorDeImportar is not null) throw ErrorDeImportar;
        return Task.FromResult(IdDeImportacion);
    }

    public Task ActualizarBorradorAsync(
        ComandoActualizarBorradorApi comando, CancellationToken token = default)
    {
        VecesActualizar++;
        UltimoComando = comando;
        if (ErrorDeActualizar is not null) throw ErrorDeActualizar;
        return Task.CompletedTask;
    }

    public Task PublicarAsync(Guid id, CancellationToken token = default)
    {
        VecesPublicar++;
        UltimoPublicado = id;
        if (ErrorDePublicar is not null) throw ErrorDePublicar;
        return Task.CompletedTask;
    }

    public Task AnularFuturaAsync(Guid id, CancellationToken token = default)
    {
        VecesAnular++;
        UltimoAnulado = id;
        if (ErrorDeAnular is not null) throw ErrorDeAnular;
        return Task.CompletedTask;
    }

    public Task DescartarBorradorAsync(Guid id, CancellationToken token = default)
    {
        VecesDescartar++;
        UltimoDescartado = id;
        if (ErrorDeDescartar is not null) throw ErrorDeDescartar;
        return Task.CompletedTask;
    }

    public Task<Stream> DescargarDocumentoOriginalAsync(
        Guid id, CancellationToken token = default)
    {
        VecesDescargar++;
        if (ErrorDeDescargar is not null) throw ErrorDeDescargar;
        return Task.FromResult<Stream>(new MemoryStream(ContenidoPdf, writable: false));
    }

    // SP9A: publicaciones de precio de huevo.
    public Exception? ErrorDeListarHuevo { get; set; }
    public Exception? ErrorDeObtenerHuevo { get; set; }
    public Exception? ErrorDePublicarHuevo { get; set; }
    public Exception? ErrorDeAnularHuevo { get; set; }
    public Exception? ErrorDeDescartarHuevo { get; set; }
    public Exception? ErrorDeActualizarHuevo { get; set; }
    public Exception? ErrorDeImportarHuevo { get; set; }
    public Exception? ErrorDeDescargarHuevo { get; set; }
    public Exception? ErrorDeVigenteHuevo { get; set; }
    public Exception? ErrorDePrevisualizarCorreccionHuevo { get; set; }
    public Exception? ErrorDeCorregirHuevo { get; set; }

    public PublicacionPrecioHuevoDetalleApi? VigenteHuevo { get; set; }
    public VistaPreviaCorreccionHuevoApi PreviaCorreccionHuevo { get; set; } = new([], 0m);

    public int VecesObtenerVigenteHuevo { get; private set; }
    public int VecesPrevisualizarCorreccionHuevo { get; private set; }
    public int VecesCorregirHuevo { get; private set; }

    public (Guid Erronea, Guid Correctiva)? UltimaPrevisualizacionHuevo { get; private set; }
    public ComandoCorregirVigenteHuevoApi? UltimoComandoCorregirHuevo { get; private set; }

    public List<PublicacionPrecioHuevoResumenApi> ResumenesHuevo { get; } = [];
    public PublicacionPrecioHuevoDetalleApi? DetalleHuevoActual { get; set; }
    public Guid IdDeImportacionHuevo { get; set; } = Guid.NewGuid();
    public byte[] ContenidoExcel { get; set; } = "PK\x03\x04 planilla de prueba"u8.ToArray();

    public int VecesListarHuevo { get; private set; }
    public int VecesObtenerHuevo { get; private set; }
    public int VecesImportarHuevo { get; private set; }
    public int VecesActualizarHuevo { get; private set; }
    public int VecesPublicarHuevo { get; private set; }
    public int VecesAnularHuevo { get; private set; }
    public int VecesDescartarHuevo { get; private set; }
    public int VecesDescargarHuevo { get; private set; }

    public Guid? UltimoPublicadoHuevo { get; private set; }
    public Guid? UltimoAnuladoHuevo { get; private set; }
    public Guid? UltimoDescartadoHuevo { get; private set; }
    public ComandoActualizarBorradorHuevoApi? UltimoComandoHuevo { get; private set; }
    public byte[]? UltimoExcelImportado { get; private set; }

    public Task<IReadOnlyList<PublicacionPrecioHuevoResumenApi>> ListarPublicacionesHuevoAsync(
        CancellationToken token = default)
    {
        VecesListarHuevo++;
        if (ErrorDeListarHuevo is not null) throw ErrorDeListarHuevo;
        return Task.FromResult<IReadOnlyList<PublicacionPrecioHuevoResumenApi>>(ResumenesHuevo);
    }

    public Task<PublicacionPrecioHuevoDetalleApi> ObtenerPublicacionHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtenerHuevo++;
        if (ErrorDeObtenerHuevo is not null) throw ErrorDeObtenerHuevo;
        return Task.FromResult(DetalleHuevoActual ?? CrearDetalleHuevo(id, "Borrador"));
    }

    public Task<Guid> ImportarExcelHuevoAsync(
        Stream contenido, string nombreArchivo, CancellationToken token = default)
    {
        VecesImportarHuevo++;
        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        UltimoExcelImportado = memoria.ToArray();
        if (ErrorDeImportarHuevo is not null) throw ErrorDeImportarHuevo;
        return Task.FromResult(IdDeImportacionHuevo);
    }

    public Task ActualizarBorradorHuevoAsync(
        ComandoActualizarBorradorHuevoApi comando, CancellationToken token = default)
    {
        VecesActualizarHuevo++;
        UltimoComandoHuevo = comando;
        if (ErrorDeActualizarHuevo is not null) throw ErrorDeActualizarHuevo;
        return Task.CompletedTask;
    }

    public Task PublicarHuevoAsync(Guid id, CancellationToken token = default)
    {
        VecesPublicarHuevo++;
        UltimoPublicadoHuevo = id;
        if (ErrorDePublicarHuevo is not null) throw ErrorDePublicarHuevo;
        return Task.CompletedTask;
    }

    public Task AnularFuturaHuevoAsync(Guid id, CancellationToken token = default)
    {
        VecesAnularHuevo++;
        UltimoAnuladoHuevo = id;
        if (ErrorDeAnularHuevo is not null) throw ErrorDeAnularHuevo;
        return Task.CompletedTask;
    }

    public Task DescartarBorradorHuevoAsync(Guid id, CancellationToken token = default)
    {
        VecesDescartarHuevo++;
        UltimoDescartadoHuevo = id;
        if (ErrorDeDescartarHuevo is not null) throw ErrorDeDescartarHuevo;
        return Task.CompletedTask;
    }

    public Task<Stream> DescargarDocumentoOriginalHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesDescargarHuevo++;
        if (ErrorDeDescargarHuevo is not null) throw ErrorDeDescargarHuevo;
        return Task.FromResult<Stream>(new MemoryStream(ContenidoExcel, writable: false));
    }

    public Task<PublicacionPrecioHuevoDetalleApi?> ObtenerPublicacionVigenteHuevoAsync(
        CancellationToken token = default)
    {
        VecesObtenerVigenteHuevo++;
        if (ErrorDeVigenteHuevo is not null) throw ErrorDeVigenteHuevo;
        return Task.FromResult(VigenteHuevo);
    }

    public Task<VistaPreviaCorreccionHuevoApi> PrevisualizarCorreccionHuevoAsync(
        Guid erroneaId, Guid correctivaId, CancellationToken token = default)
    {
        VecesPrevisualizarCorreccionHuevo++;
        UltimaPrevisualizacionHuevo = (erroneaId, correctivaId);
        if (ErrorDePrevisualizarCorreccionHuevo is not null) throw ErrorDePrevisualizarCorreccionHuevo;
        return Task.FromResult(PreviaCorreccionHuevo);
    }

    public Task CorregirVigenteHuevoAsync(
        Guid erroneaId, Guid correctivaId, string motivo, CancellationToken token = default)
    {
        VecesCorregirHuevo++;
        UltimoComandoCorregirHuevo = new ComandoCorregirVigenteHuevoApi(erroneaId, correctivaId, motivo);
        if (ErrorDeCorregirHuevo is not null) throw ErrorDeCorregirHuevo;
        return Task.CompletedTask;
    }

    public Exception? ErrorDeListarPedidos { get; set; }
    public Exception? ErrorDeObtenerPedido { get; set; }
    public Exception? ErrorDeDecision { get; set; }
    public Exception? ErrorDeNotificaciones { get; set; }
    public Exception? ErrorDeMarcarLeida { get; set; }

    public PaginaPedidosApi PaginaDePedidos { get; set; } = new([], 0, 1, 20);
    public PedidoDetalleApi? PedidoActual { get; set; }
    public BandejaNotificacionesApi NotificacionesDePedidos { get; set; } = new([], 0);

    public int VecesListarPedidos { get; private set; }
    public int VecesObtenerPedido { get; private set; }
    public int VecesDevolver { get; private set; }
    public int VecesRechazar { get; private set; }
    public int VecesAceptar { get; private set; }
    public int VecesActualizarEntrega { get; private set; }
    public int VecesListarNotificaciones { get; private set; }
    public int VecesMarcarLeida { get; private set; }

    public FiltrosPedidosApi? UltimosFiltros { get; private set; }
    public Guid? UltimoPedidoObtenido { get; private set; }
    public (Guid Id, string Motivo)? UltimaDecisionConMotivo { get; private set; }
    public (Guid Id, DateOnly Fecha)? UltimaFechaEntrega { get; private set; }
    public Guid? UltimaNotificacionMarcada { get; private set; }

    public Task<PaginaPedidosApi> ListarPedidosAsync(
        FiltrosPedidosApi filtros, CancellationToken token = default)
    {
        VecesListarPedidos++;
        UltimosFiltros = filtros;
        if (ErrorDeListarPedidos is not null) throw ErrorDeListarPedidos;
        return Task.FromResult(PaginaDePedidos);
    }

    public Task<PedidoDetalleApi> ObtenerPedidoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtenerPedido++;
        UltimoPedidoObtenido = id;
        if (ErrorDeObtenerPedido is not null) throw ErrorDeObtenerPedido;
        return Task.FromResult(PedidoActual ?? CrearPedido(id, "Solicitado"));
    }

    public Exception? ErrorDeObtenerCredito { get; set; }
    public CreditoHuevoPedidoApi? CreditoDePedido { get; set; }
    public int VecesObtenerCredito { get; private set; }
    public Guid? UltimoCreditoPedido { get; private set; }

    public Task<CreditoHuevoPedidoApi> ObtenerCreditoDePedidoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtenerCredito++;
        UltimoCreditoPedido = id;
        if (ErrorDeObtenerCredito is not null) throw ErrorDeObtenerCredito;
        return Task.FromResult(CreditoDePedido ?? CrearCredito());
    }

    public Task DevolverPedidoAsync(Guid id, string motivo, CancellationToken token = default)
    {
        VecesDevolver++;
        UltimaDecisionConMotivo = (id, motivo);
        if (ErrorDeDecision is not null) throw ErrorDeDecision;
        return Task.CompletedTask;
    }

    public Task RechazarPedidoAsync(Guid id, string motivo, CancellationToken token = default)
    {
        VecesRechazar++;
        UltimaDecisionConMotivo = (id, motivo);
        if (ErrorDeDecision is not null) throw ErrorDeDecision;
        return Task.CompletedTask;
    }

    public Task AceptarPedidoAsync(
        Guid id, DateOnly fechaEntregaEstimada, CancellationToken token = default)
    {
        VecesAceptar++;
        UltimaFechaEntrega = (id, fechaEntregaEstimada);
        if (ErrorDeDecision is not null) throw ErrorDeDecision;
        return Task.CompletedTask;
    }

    public Task ActualizarEntregaEstimadaAsync(
        Guid id, DateOnly nuevaFecha, CancellationToken token = default)
    {
        VecesActualizarEntrega++;
        UltimaFechaEntrega = (id, nuevaFecha);
        if (ErrorDeDecision is not null) throw ErrorDeDecision;
        return Task.CompletedTask;
    }

    public Task<BandejaNotificacionesApi> ListarNotificacionesPedidoAsync(
        CancellationToken token = default)
    {
        VecesListarNotificaciones++;
        if (ErrorDeNotificaciones is not null) throw ErrorDeNotificaciones;
        return Task.FromResult(NotificacionesDePedidos);
    }

    public Task MarcarNotificacionPedidoLeidaAsync(Guid id, CancellationToken token = default)
    {
        VecesMarcarLeida++;
        UltimaNotificacionMarcada = id;
        if (ErrorDeMarcarLeida is not null) throw ErrorDeMarcarLeida;
        return Task.CompletedTask;
    }

    // SP8C/SP8D: despacho, respaldo del receptor y su descarga para el detalle,
    // y el recibo imprimible del despacho.
    public Exception? ErrorDeDespachar { get; set; }
    public Exception? ErrorDeDocumentoNota { get; set; }
    public Exception? ErrorDeRecibo { get; set; }

    public byte[] ContenidoNota { get; set; } = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    public int VecesDespachar { get; private set; }
    public int VecesDescargarNota { get; private set; }
    public int VecesDescargarRecibo { get; private set; }

    public ComandoDespachoApi? UltimoDespacho { get; private set; }
    public (Guid Id, Guid Documento)? UltimaDescargaNota { get; private set; }
    public Guid? UltimoReciboPedido { get; private set; }

    public Task DespacharPedidoAsync(ComandoDespachoApi comando, CancellationToken token = default)
    {
        VecesDespachar++;
        UltimoDespacho = comando;
        if (ErrorDeDespachar is not null) throw ErrorDeDespachar;
        return Task.CompletedTask;
    }

    public Task<(Stream Contenido, string TipoContenido)> DescargarDocumentoNotaAsync(
        Guid id, Guid documentoId, CancellationToken token = default)
    {
        VecesDescargarNota++;
        UltimaDescargaNota = (id, documentoId);
        if (ErrorDeDocumentoNota is not null) throw ErrorDeDocumentoNota;
        return Task.FromResult(
            (new MemoryStream(ContenidoNota, writable: false) as Stream, "image/jpeg"));
    }

    public Task<Stream> ObtenerReciboPdfAsync(Guid id, CancellationToken token = default)
    {
        VecesDescargarRecibo++;
        UltimoReciboPedido = id;
        if (ErrorDeRecibo is not null) throw ErrorDeRecibo;
        return Task.FromResult<Stream>(new MemoryStream(
            "%PDF-1.7 recibo de despacho"u8.ToArray(), writable: false));
    }

    // SP9C: bandeja de recepción de huevo de CAISY (confirmar recepción y
    // recibo del despacho de huevo).
    public Exception? ErrorDeListarDespachosHuevo { get; set; }
    public Exception? ErrorDeObtenerDespachoHuevo { get; set; }
    public Exception? ErrorDeConfirmarRecepcion { get; set; }
    public Exception? ErrorDeReciboHuevo { get; set; }
    public Exception? ErrorDeNotificacionesHuevo { get; set; }
    public Exception? ErrorDeMarcarLeidaHuevo { get; set; }

    public PaginaDespachosHuevoApi PaginaDeDespachosHuevo { get; set; } = new([], 0);
    public DespachoHuevoDetalleApi? DespachoHuevoActual { get; set; }
    public BandejaNotificacionesDespachoHuevoApi NotificacionesDeDespachosHuevo { get; set; } = new([], 0);

    public int VecesListarDespachosHuevo { get; private set; }
    public int VecesObtenerDespachoHuevo { get; private set; }
    public int VecesConfirmarRecepcion { get; private set; }
    public int VecesDescargarReciboHuevo { get; private set; }
    public int VecesListarNotificacionesHuevo { get; private set; }
    public int VecesMarcarLeidaHuevo { get; private set; }

    public FiltrosDespachosHuevoApi? UltimosFiltrosDespachosHuevo { get; private set; }
    public Guid? UltimoDespachoHuevoObtenido { get; private set; }
    public Guid? UltimaRecepcionConfirmada { get; private set; }
    public Guid? UltimoReciboHuevo { get; private set; }
    public Guid? UltimaNotificacionHuevoMarcada { get; private set; }

    public Task<PaginaDespachosHuevoApi> ListarDespachosHuevoAsync(
        FiltrosDespachosHuevoApi filtros, CancellationToken token = default)
    {
        VecesListarDespachosHuevo++;
        UltimosFiltrosDespachosHuevo = filtros;
        if (ErrorDeListarDespachosHuevo is not null) throw ErrorDeListarDespachosHuevo;
        return Task.FromResult(PaginaDeDespachosHuevo);
    }

    public Task<DespachoHuevoDetalleApi> ObtenerDespachoHuevoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtenerDespachoHuevo++;
        UltimoDespachoHuevoObtenido = id;
        if (ErrorDeObtenerDespachoHuevo is not null) throw ErrorDeObtenerDespachoHuevo;
        return Task.FromResult(DespachoHuevoActual ?? CrearDespachoHuevo(id, "Despachado"));
    }

    public Task ConfirmarRecepcionDespachoHuevoAsync(Guid id, CancellationToken token = default)
    {
        VecesConfirmarRecepcion++;
        UltimaRecepcionConfirmada = id;
        if (ErrorDeConfirmarRecepcion is not null) throw ErrorDeConfirmarRecepcion;
        return Task.CompletedTask;
    }

    public Task<Stream> ObtenerReciboDespachoHuevoPdfAsync(Guid id, CancellationToken token = default)
    {
        VecesDescargarReciboHuevo++;
        UltimoReciboHuevo = id;
        if (ErrorDeReciboHuevo is not null) throw ErrorDeReciboHuevo;
        return Task.FromResult<Stream>(new MemoryStream(
            "%PDF-1.7 recibo de recepcion de huevo"u8.ToArray(), writable: false));
    }

    public Task<BandejaNotificacionesDespachoHuevoApi> ListarNotificacionesDespachoHuevoAsync(
        CancellationToken token = default)
    {
        VecesListarNotificacionesHuevo++;
        if (ErrorDeNotificacionesHuevo is not null) throw ErrorDeNotificacionesHuevo;
        return Task.FromResult(NotificacionesDeDespachosHuevo);
    }

    public Task MarcarNotificacionDespachoHuevoLeidaAsync(Guid id, CancellationToken token = default)
    {
        VecesMarcarLeidaHuevo++;
        UltimaNotificacionHuevoMarcada = id;
        if (ErrorDeMarcarLeidaHuevo is not null) throw ErrorDeMarcarLeidaHuevo;
        return Task.CompletedTask;
    }

    public static DespachoHuevoDetalleApi CrearDespachoHuevo(
        Guid id, string estado = "Despachado") =>
        new(
            id, estado, new(2025, 11, 2), 10, 2950, 1602.75m,
            [
                new DetalleDespachoHuevoApi(
                    Guid.NewGuid(), "Primera", 10, 0, 2950, 0.5433m, 1602.75m),
            ]);

    public static PedidoDetalleApi CrearPedido(
        Guid id, string estado = "Solicitado", DateOnly? fechaEntregaEstimada = null,
        EntregaPedidoApi? entrega = null, RecepcionPedidoApi? recepcion = null) =>
        new(
            id, Guid.NewGuid(), estado, new(2025, 11, 2), fechaEntregaEstimada, 14162.5m,
            [
                new LineaPedidoApi(
                    Guid.NewGuid(), "PosturaUno", "Bolsa", 80, 80, 176.5m, 14120m, Guid.NewGuid()),
            ],
            [
                new TransicionPedidoApi(
                    "Borrador", "Solicitado", new(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc), null, null),
            ],
            entrega, recepcion);

    public static EntregaPedidoApi CrearEntrega(Guid pedidoId) => new(
        "NOTA-77", new(2025, 11, 1), new(2025, 11, 2), 14100m, 14120m,
        [new LineaEntregaApi("PosturaUno", 80, 80)],
        [new DocumentoNotaApi(Guid.NewGuid(), "nota-frente.jpg", "image/jpeg", 1024)]);

    // Saldo negativo por defecto: es el caso interesante para la decisión de
    // CAISY. Las cifras coinciden con el pedido de CrearPedido (14 120 de la
    // única línea congelada).
    public static CreditoHuevoPedidoApi CrearCredito(
        decimal saldo = -5000m, decimal montoDelPedido = 14120m, bool computado = true) =>
        new(saldo, montoDelPedido, saldo + montoDelPedido, computado,
            [
                new AjusteCreditoHuevoApi(
                    Guid.NewGuid(), 45m, "Corrección de precio Extra.", new(2026, 9, 1)),
            ]);

    public static RecepcionPedidoApi CrearRecepcion() => new(
        new(2025, 11, 3), 14120m,
        [new LineaRecepcionApi("PosturaUno", 80, 80)],
        [new DiferenciaRecepcionApi("PosturaUno", 78, 80, -2)]);

    public static NotificacionPreciosDetalleApi CrearDetalle(
        Guid id, string estado = "Borrador", string vigenteDesde = "2025-12-01") =>
        new(
            id, new(2025, 11, 2), DateOnly.Parse(vigenteDesde, CultureInfo.InvariantCulture), estado,
            1.20m, 0.60m, 0.75m, Guid.NewGuid(),
            [
                new DetallePrecioApi(
                    Guid.NewGuid(), "Preiniciador", "Bolsa", 118.50m, 115.00m, 1, 21),
                new DetallePrecioApi(
                    Guid.NewGuid(), "PosturaDos", "Granel", 112.75m, 110.25m, null, null),
            ]);

    public static PublicacionPrecioHuevoDetalleApi CrearDetalleHuevo(
        Guid id, string estado = "Borrador", string fechaVigencia = "2025-12-01") =>
        new(
            id, new(2025, 11, 2), DateOnly.Parse(fechaVigencia, CultureInfo.InvariantCulture), estado,
            0.50m, Guid.NewGuid(),
            [
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Primera", 0.045m, 0.044m, 0.545m),
                new DetallePrecioHuevoApi(
                    Guid.NewGuid(), "Extra", 0.050m, 0.049m, 0.550m),
            ]);
}
