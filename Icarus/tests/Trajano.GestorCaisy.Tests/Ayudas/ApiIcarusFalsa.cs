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
    public int VecesDescargar { get; private set; }

    public Guid? UltimoObtenido { get; private set; }
    public Guid? UltimoPublicado { get; private set; }
    public Guid? UltimoAnulado { get; private set; }
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

    public Task<Stream> DescargarDocumentoOriginalAsync(
        Guid id, CancellationToken token = default)
    {
        VecesDescargar++;
        if (ErrorDeDescargar is not null) throw ErrorDeDescargar;
        return Task.FromResult<Stream>(new MemoryStream(ContenidoPdf, writable: false));
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

    // SP8C: despacho, respaldos de la nota y su descarga para el detalle.
    public Exception? ErrorDeDespachar { get; set; }
    public Exception? ErrorDeDocumentoNota { get; set; }

    public byte[] ContenidoNota { get; set; } = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    public int VecesDespachar { get; private set; }
    public int VecesSubirDocumentoNota { get; private set; }
    public int VecesDescargarNota { get; private set; }

    public ComandoDespachoApi? UltimoDespacho { get; private set; }
    public (Guid Id, string Nombre, Guid? Reemplaza)? UltimoDocumentoNota { get; private set; }
    public (Guid Id, Guid Documento)? UltimaDescargaNota { get; private set; }
    public Guid IdDocumentoNota { get; set; } = Guid.NewGuid();

    public Task DespacharPedidoAsync(ComandoDespachoApi comando, CancellationToken token = default)
    {
        VecesDespachar++;
        UltimoDespacho = comando;
        if (ErrorDeDespachar is not null) throw ErrorDeDespachar;
        return Task.CompletedTask;
    }

    public Task<Guid> SubirDocumentoNotaAsync(
        Guid id, Stream contenido, string nombreArchivo,
        Guid? reemplazaDocumentoId, CancellationToken token = default)
    {
        VecesSubirDocumentoNota++;
        UltimoDocumentoNota = (id, nombreArchivo, reemplazaDocumentoId);
        if (ErrorDeDocumentoNota is not null) throw ErrorDeDocumentoNota;
        return Task.FromResult(IdDocumentoNota);
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
        [new DocumentoNotaApi(Guid.NewGuid(), "nota-frente.jpg", "image/jpeg", 1024, true)]);

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
}
