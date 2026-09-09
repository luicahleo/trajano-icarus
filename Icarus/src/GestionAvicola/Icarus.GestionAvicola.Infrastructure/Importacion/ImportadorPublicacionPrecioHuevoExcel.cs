using System.Globalization;
using ClosedXML.Excel;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Importa el formato real de CAISY para el cambio de precio de huevo (spec
// SP9). Ignora deliberadamente "Precio Unitario" y "Diferencia Al Productor":
// son fórmulas sin valor en caché en el documento real.
public sealed class ImportadorPublicacionPrecioHuevoExcel : IImportadorPublicacionPrecioHuevoExcel
{
    private static readonly IReadOnlyDictionary<string, TamanoHuevo> Tamanos = new Dictionary<string, TamanoHuevo>
    {
        ["EXTRA"] = TamanoHuevo.Extra,
        ["PRIMERA"] = TamanoHuevo.Primera,
        ["SEGUNDA"] = TamanoHuevo.Segunda,
        ["TERCERA"] = TamanoHuevo.Tercera,
        ["CUARTA"] = TamanoHuevo.Cuarta,
        ["QUINTA"] = TamanoHuevo.Quinta,
    };

    public ResultadoImportacionPrecioHuevo Importar(Stream contenido)
    {
        try
        {
            using var libro = new XLWorkbook(contenido);
            var hoja = libro.Worksheets.FirstOrDefault();
            var usados = hoja?.RangeUsed();
            if (usados is null) return Error("El archivo no contiene datos.");

            DateOnly? fechaNotificacion = null;
            DateOnly? fechaVigencia = null;
            foreach (var fila in usados.Rows())
            {
                var primera = Normalizar(fila.Cell(1).GetString());
                if (primera.StartsWith("FECHA DE NOTIFICACION", StringComparison.Ordinal))
                    fechaNotificacion = LeerFecha(fila.Cell(2));
                else if (primera.StartsWith("FECHA DE ENTRADA EN VIGENCIA", StringComparison.Ordinal))
                    fechaVigencia = LeerFecha(fila.Cell(2));
            }

            var errores = new List<ErrorImportacionPrecioHuevo>();
            var encabezado = usados.Rows().FirstOrDefault(r =>
                r.CellsUsed().Any(c => Normalizar(c.GetString()) == "TAMANO"));
            if (encabezado is null)
                errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la cabecera de la tabla de precios."));

            var detalles = new List<DatosDetallePrecioHuevo>();
            decimal? servicioComun = null;
            if (encabezado is not null)
            {
                var columnas = Columnas(encabezado);
                foreach (var fila in usados.Rows().Where(r => r.RowNumber() > encabezado.RowNumber()))
                {
                    var tamanoTexto = Normalizar(Texto(fila, columnas.Tamano));
                    if (string.IsNullOrWhiteSpace(tamanoTexto)) continue;
                    if (!Tamanos.TryGetValue(tamanoTexto, out var tamano))
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), $"El tamaño '{tamanoTexto}' no es reconocido.")); continue; }

                    var precioProductor = DecimalCelda(fila, columnas.PrecioProductor);
                    if (precioProductor is null or <= 0)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El precio al productor debe ser mayor que cero.")); continue; }

                    var servicio = DecimalCelda(fila, columnas.Servicio);
                    if (servicio is null or <= 0)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El servicio debe ser mayor que cero.")); continue; }
                    if (servicioComun is null) servicioComun = servicio;
                    else if (servicioComun != servicio)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El servicio debe ser el mismo para todos los tamaños.")); continue; }

                    detalles.Add(new DatosDetallePrecioHuevo(
                        tamano, precioProductor.Value, DecimalCelda(fila, columnas.PrecioActual)));
                }
            }
            if (fechaNotificacion is null) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la fecha de notificación."));
            if (fechaVigencia is null) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la fecha de entrada en vigencia."));
            if (detalles.Count == 0) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontraron filas de precio."));
            if (errores.Count > 0) return new ResultadoImportacionPrecioHuevo(null, errores);

            var propuesta = new DatosPublicacionPrecioHuevo(
                fechaNotificacion!.Value, fechaVigencia!.Value, servicioComun!.Value, detalles);
            return new ResultadoImportacionPrecioHuevo(propuesta, []);
        }
        catch (Exception)
        {
            return Error("El archivo no se pudo leer como libro de Excel.");
        }
    }

    private static (int? Tamano, int? PrecioActual, int? PrecioProductor, int? Servicio) Columnas(IXLRangeRow fila)
    {
        int? tamano = null, actual = null, productor = null, servicio = null;
        foreach (var celda in fila.CellsUsed())
        {
            var nombre = Normalizar(celda.GetString());
            if (nombre == "TAMANO") tamano = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("PRECIO ACTUAL")) actual = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("NUEVO PRECIO AL PRODUCTOR")) productor = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("SERVICIOS")) servicio = celda.Address.ColumnNumber;
        }
        return (tamano, actual, productor, servicio);
    }

    private static string Texto(IXLRangeRow fila, int? columna) => columna is null ? "" : fila.Cell(columna.Value).GetString().Trim();
    private static decimal? DecimalCelda(IXLRangeRow fila, int? columna)
    {
        if (columna is null) return null;
        var celda = fila.Cell(columna.Value);
        if (celda.TryGetValue<decimal>(out var valor)) return valor;
        return decimal.TryParse(celda.GetString().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out valor) ? valor : null;
    }
    private static DateOnly? LeerFecha(IXLCell celda)
    {
        if (celda.TryGetValue<DateTime>(out var fecha)) return DateOnly.FromDateTime(fecha);
        return DateTime.TryParseExact(celda.GetString().Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ? DateOnly.FromDateTime(fecha) : null;
    }
    private static ResultadoImportacionPrecioHuevo Error(string mensaje) => new(null, [new ErrorImportacionPrecioHuevo(null, mensaje)]);
    private static string Normalizar(string texto)
    {
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(c));
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
