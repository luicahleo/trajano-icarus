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
