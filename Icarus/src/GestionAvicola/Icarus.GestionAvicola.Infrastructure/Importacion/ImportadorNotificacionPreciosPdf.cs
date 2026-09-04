using System.Globalization;
using System.Text.RegularExpressions;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using UglyToad.PdfPig;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Importador determinista del PDF de Notificación de Precios (spec SP8),
// detrás de IImportadorNotificacionPreciosPdf: PdfPig solo vive en
// Infrastructure. Devuelve la propuesta completa o la lista de errores; nunca
// precios parciales. El contenido del documento no se registra en logs.
public sealed partial class ImportadorNotificacionPreciosPdf : IImportadorNotificacionPreciosPdf
{
    [GeneratedRegex(@"^FECHA:\s*(\d{2}/\d{2}/\d{4})\s*$")]
    private static partial Regex PatronFecha();

    [GeneratedRegex(@"^VIGENTE DESDE:\s*(\d{2}/\d{2}/\d{4})\s*$")]
    private static partial Regex PatronVigencia();

    [GeneratedRegex(@"^APORTE CAISY:\s*([\d.,]+)\s*$")]
    private static partial Regex PatronAporteCaisy();

    [GeneratedRegex(@"^FONDO:\s*([\d.,]+)\s*$")]
    private static partial Regex PatronFondo();

    [GeneratedRegex(@"^SERVICIOS:\s*([\d.,]+)\s*$")]
    private static partial Regex PatronServicios();

    // Fila de precio: código SJ-x, presentación, edades (o guion) y los dos
    // importes: «Precio actual» y «Nuevo precio».
    [GeneratedRegex(@"^(SJ-[A-Z0-9]+)\s+(B|G)\s+(\d{1,3}|-)\s+(\d{1,3}|-)\s+([\d.,]+)\s+([\d.,]+)\s*$")]
    private static partial Regex PatronFila();

    private static readonly Dictionary<string, TipoAlimento> TiposPorCodigo = new()
    {
        ["SJ-PRE"] = TipoAlimento.Preiniciador,
        ["SJ-1"] = TipoAlimento.Iniciador,
        ["SJ-2"] = TipoAlimento.Crecimiento,
        ["SJ-3"] = TipoAlimento.Finalizador,
        ["SJ-P1"] = TipoAlimento.PosturaUno,
        ["SJ-P2"] = TipoAlimento.PosturaDos,
    };

    public ResultadoImportacionPdf Importar(Stream contenido)
    {
        var errores = new List<ErrorImportacionPdf>();
        List<string> lineas;
        try
        {
            using var documento = PdfDocument.Open(contenido);
            if (documento.NumberOfPages == 0)
                return SoloError("El documento no tiene páginas legibles.");
            lineas = ExtraerLineas(documento.GetPage(1));
        }
        catch (Exception)
        {
            return SoloError("El archivo no se pudo leer como documento PDF.");
        }

        DateOnly? fechaDocumento = null;
        DateOnly? vigenteDesde = null;
        decimal? aporteCaisy = null;
        decimal? fondo = null;
        decimal? servicios = null;
        var filas = new List<(int Numero, string Codigo, string Presentacion,
            int? EdadDesde, int? EdadHasta, decimal PrecioActual, decimal PrecioNuevo)>();

        foreach (var linea in lineas)
        {
            if (linea.Contains("PRECIO ACTUAL", StringComparison.Ordinal)
                && linea.Contains("NUEVO PRECIO", StringComparison.Ordinal))
                continue; // Encabezado de la tabla.
            if (PatronFecha().Match(linea) is { Success: true } fecha)
            {
                fechaDocumento = ParsearFecha(fecha.Groups[1].Value);
                continue;
            }
            if (PatronVigencia().Match(linea) is { Success: true } vigencia)
            {
                vigenteDesde = ParsearFecha(vigencia.Groups[1].Value);
                continue;
            }
            if (PatronAporteCaisy().Match(linea) is { Success: true } aporte)
            {
                aporteCaisy = ParsearImporte(aporte.Groups[1].Value);
                continue;
            }
            if (PatronFondo().Match(linea) is { Success: true } fondoCoincidencia)
            {
                fondo = ParsearImporte(fondoCoincidencia.Groups[1].Value);
                continue;
            }
            if (PatronServicios().Match(linea) is { Success: true } serviciosCoincidencia)
            {
                servicios = ParsearImporte(serviciosCoincidencia.Groups[1].Value);
                continue;
            }
            if (PatronFila().Match(linea) is { Success: true } fila)
                ProcesarFila(filas, errores, fila);
        }

        if (fechaDocumento is null)
            errores.Add(new ErrorImportacionPdf(null, "No se encontró la fecha del documento."));
        if (vigenteDesde is null)
            errores.Add(new ErrorImportacionPdf(null, "No se encontró la vigencia del documento."));
        if (aporteCaisy is null || fondo is null || servicios is null)
            errores.Add(new ErrorImportacionPdf(null, "No se encontraron los aportes del documento."));
        if (filas.Count == 0)
            errores.Add(new ErrorImportacionPdf(null, "No se encontraron filas de precio."));

        if (errores.Count > 0)
            return new ResultadoImportacionPdf(null, errores);

        var propuesta = new DatosNotificacionPdf(
            fechaDocumento!.Value, vigenteDesde!.Value,
            aporteCaisy!.Value, fondo!.Value, servicios!.Value,
            filas.Select(f => new DatosDetallePrecio(
                TiposPorCodigo[f.Codigo],
                f.Presentacion == "B" ? PresentacionAlimento.Bolsa : PresentacionAlimento.Granel,
                f.PrecioNuevo, f.EdadDesde, f.EdadHasta, f.PrecioActual)).ToList());
        return new ResultadoImportacionPdf(propuesta, []);
    }

    private static void ProcesarFila(
        List<(int Numero, string Codigo, string Presentacion, int? EdadDesde, int? EdadHasta,
            decimal PrecioActual, decimal PrecioNuevo)> filas,
        List<ErrorImportacionPdf> errores, Match fila)
    {
        var codigo = fila.Groups[1].Value;
        var numero = filas.Count + 1;
        if (!TiposPorCodigo.ContainsKey(codigo))
        {
            errores.Add(new ErrorImportacionPdf(numero, "El código de tipo de alimento no es reconocido."));
            return;
        }
        var edadDesde = fila.Groups[3].Value == "-" ? null : (int?)int.Parse(fila.Groups[3].Value, CultureInfo.InvariantCulture);
        var edadHasta = fila.Groups[4].Value == "-" ? null : (int?)int.Parse(fila.Groups[4].Value, CultureInfo.InvariantCulture);
        filas.Add((numero, codigo, fila.Groups[2].Value,
            edadDesde, edadHasta,
            ParsearImporte(fila.Groups[5].Value), ParsearImporte(fila.Groups[6].Value)));
    }

    private static ResultadoImportacionPdf SoloError(string mensaje) =>
        new(null, [new ErrorImportacionPdf(null, mensaje)]);

    private static DateOnly ParsearFecha(string texto) =>
        DateOnly.ParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static decimal ParsearImporte(string texto) =>
        decimal.Parse(texto.Replace(',', '.'), CultureInfo.InvariantCulture);

    // Agrupa las palabras de la página por línea de base (tolerancia de 2 pt)
    // y las une con un espacio: la extracción no depende del ancho de columnas.
    private static List<string> ExtraerLineas(UglyToad.PdfPig.Content.Page pagina) =>
        pagina.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 2.0))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(" ", g
                .OrderBy(w => w.BoundingBox.Left)
                .Select(w => w.Text)))
            .ToList();
}
