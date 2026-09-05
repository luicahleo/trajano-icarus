using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Importa el formato real de CAISY: los códigos distinguen tipo y presentación,
// la edad llega como rango textual y los tres aportes están en la nota final.
public sealed partial class ImportadorNotificacionPreciosExcel : IImportadorNotificacionPreciosExcel
{
    private static readonly Dictionary<string, (TipoAlimento Tipo, PresentacionAlimento Presentacion)> Productos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SJ-PRE"] = (TipoAlimento.Preiniciador, PresentacionAlimento.Bolsa),
        ["SJ-PREG"] = (TipoAlimento.Preiniciador, PresentacionAlimento.Granel),
        ["SJ-1B"] = (TipoAlimento.Iniciador, PresentacionAlimento.Bolsa),
        ["SJ-1G"] = (TipoAlimento.Iniciador, PresentacionAlimento.Granel),
        ["SJ-2B"] = (TipoAlimento.Crecimiento, PresentacionAlimento.Bolsa),
        ["SJ-2G"] = (TipoAlimento.Crecimiento, PresentacionAlimento.Granel),
        ["SJ-3B"] = (TipoAlimento.Finalizador, PresentacionAlimento.Bolsa),
        ["SJ-3G"] = (TipoAlimento.Finalizador, PresentacionAlimento.Granel),
        ["SJ-P1B"] = (TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa),
        ["SJ-P1G"] = (TipoAlimento.PosturaUno, PresentacionAlimento.Granel),
        ["SJ-P2B"] = (TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa),
        ["SJ-P2G"] = (TipoAlimento.PosturaDos, PresentacionAlimento.Granel),
    };

    [GeneratedRegex(@"RESERVA DE UTILIZACION\s*\(BS\.\s*([\d.,]+)\).*?APORTE/CUOTA\s*\(BS\.\s*([\d.,]+)\).*?COMISION DE PROCESAMIENTO\s*\(BS\.\s*([\d.,]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PatronAportes();

    public ResultadoImportacionPdf Importar(Stream contenido)
    {
        try
        {
            using var libro = new XLWorkbook(contenido);
            var hoja = libro.Worksheets.FirstOrDefault();
            var usados = hoja?.RangeUsed();
            if (usados is null) return Error("El archivo no contiene datos.");

            DateOnly? fechaDocumento = null;
            DateOnly? vigenteDesde = null;
            var textoCompleto = new StringBuilder();
            foreach (var fila in usados.Rows())
            {
                var primera = Normalizar(fila.Cell(1).GetString());
                if (primera.StartsWith("FECHA DE NOTIFICACION", StringComparison.Ordinal))
                    fechaDocumento = LeerFecha(fila.Cell(2));
                else if (primera.StartsWith("FECHA DE APLICACION", StringComparison.Ordinal))
                    vigenteDesde = LeerFecha(fila.Cell(2));
                textoCompleto.Append(' ').Append(string.Join(' ', fila.CellsUsed().Select(c => c.GetString())));
            }

            var aportes = PatronAportes().Match(Normalizar(textoCompleto.ToString()));
            var errores = new List<ErrorImportacionPdf>();
            if (!aportes.Success)
                errores.Add(new ErrorImportacionPdf(null, "No se encontraron los aportes, fondo y servicios en la nota del documento."));
            var encabezado = usados.Rows().FirstOrDefault(r =>
                r.CellsUsed().Any(c => Normalizar(c.GetString()) == "CODIGO") &&
                r.CellsUsed().Any(c => Normalizar(c.GetString()).StartsWith("PRECIO NUEVO")));
            if (encabezado is null)
                errores.Add(new ErrorImportacionPdf(7, "No se encontró la cabecera de la tabla de precios."));

            var detalles = new List<DatosDetallePrecio>();
            if (encabezado is not null)
            {
                var columnas = Columnas(encabezado);
                foreach (var fila in usados.Rows().Where(r => r.RowNumber() > encabezado.RowNumber()))
                {
                    var codigo = Texto(fila, columnas.Codigo);
                    if (string.IsNullOrWhiteSpace(codigo)) continue;
                    if (Normalizar(codigo).StartsWith("NOTA", StringComparison.Ordinal)) continue;
                    if (!Productos.TryGetValue(codigo, out var producto))
                    { errores.Add(new ErrorImportacionPdf(fila.RowNumber(), $"El código de producto '{codigo}' no es reconocido.")); continue; }
                    var nuevo = DecimalCelda(fila, columnas.PrecioNuevo);
                    if (nuevo is null or <= 0)
                    { errores.Add(new ErrorImportacionPdf(fila.RowNumber(), "El precio nuevo debe ser mayor que cero.")); continue; }
                    var (edadDesde, edadHasta) = Edades(Texto(fila, columnas.Edad));
                    detalles.Add(new DatosDetallePrecio(producto.Tipo, producto.Presentacion, nuevo.Value,
                        edadDesde, edadHasta, DecimalCelda(fila, columnas.PrecioActual)));
                }
            }
            if (fechaDocumento is null) errores.Add(new ErrorImportacionPdf(null, "No se encontró la fecha de notificación."));
            if (vigenteDesde is null) errores.Add(new ErrorImportacionPdf(null, "No se encontró la fecha de aplicación."));
            if (detalles.Count == 0) errores.Add(new ErrorImportacionPdf(null, "No se encontraron filas de precio."));
            if (errores.Count > 0) return new ResultadoImportacionPdf(null, errores);

            var propuesta = new DatosNotificacionPdf(fechaDocumento!.Value, vigenteDesde!.Value,
                DecimalTexto(aportes.Groups[2].Value), DecimalTexto(aportes.Groups[1].Value), DecimalTexto(aportes.Groups[3].Value), detalles);
            return new ResultadoImportacionPdf(propuesta, []);
        }
        catch (Exception)
        {
            return Error("El archivo no se pudo leer como libro de Excel.");
        }
    }

    private static (int? Codigo, int? Edad, int? PrecioActual, int? PrecioNuevo) Columnas(IXLRangeRow fila)
    {
        int? codigo = null, edad = null, actual = null, nuevo = null;
        foreach (var celda in fila.CellsUsed())
        {
            var nombre = Normalizar(celda.GetString());
            if (nombre == "CODIGO") codigo = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("EDAD DE ALIMENTACION")) edad = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("PRECIO ACTUAL")) actual = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("PRECIO NUEVO")) nuevo = celda.Address.ColumnNumber;
        }
        return (codigo, edad, actual, nuevo);
    }

    private static (int? Desde, int? Hasta) Edades(string texto)
    {
        var numeros = Regex.Matches(texto, @"\d+").Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
        return numeros.Length switch { 0 => (null, null), 1 => (numeros[0], null), _ => (numeros[0], numeros[1]) };
    }

    private static decimal DecimalTexto(string texto) => decimal.Parse(texto.Replace(',', '.'), CultureInfo.InvariantCulture);
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
    private static ResultadoImportacionPdf Error(string mensaje) => new(null, [new ErrorImportacionPdf(null, mensaje)]);
    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(c));
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
