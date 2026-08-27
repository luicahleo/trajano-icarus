using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Icarus.GestionAvicola.Application.Vacunacion;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Parseo tolerante del Excel del papel de CAISY (spec SP7): columnas FECHA
// (se ignora: la fuente de verdad es EDAD), EDAD, VACUNA, MODO DE APLICACION
// y OBSERVACIONES, con nombres tolerantes a mayúsculas, tildes y espacios.
// No decide el all-or-nothing: devuelve ítems y errores por fila; el handler
// rechaza la importación completa si hay errores.
public sealed class ImportadorCronogramaVacunacion : IImportadorCronogramaVacunacion
{
    public ResultadoImportacionCronograma Importar(Stream contenido)
    {
        var items = new List<ItemCronogramaImportado>();
        var errores = new List<ErrorFilaImportacion>();
        using var libro = new XLWorkbook(contenido);
        var hoja = libro.Worksheets.FirstOrDefault();
        if (hoja is null)
        {
            errores.Add(new ErrorFilaImportacion(1, "El archivo no contiene hojas de cálculo."));
            return new ResultadoImportacionCronograma(items, errores);
        }

        var columnas = IndicesColumnas(hoja.Row(1));
        if (columnas.Edad is null || columnas.Vacuna is null)
        {
            errores.Add(new ErrorFilaImportacion(1, "Faltan las columnas EDAD y VACUNA en el encabezado."));
            return new ResultadoImportacionCronograma(items, errores);
        }

        var fila = 2;
        while (!hoja.Row(fila).IsEmpty())
        {
            var edadTexto = TextoCelda(hoja, fila, columnas.Edad);
            var vacuna = TextoCelda(hoja, fila, columnas.Vacuna);
            var modo = TextoCelda(hoja, fila, columnas.ModoAplicacion);
            var observaciones = TextoCelda(hoja, fila, columnas.Observaciones);

            var valida = true;
            var edad = 0;
            if (!int.TryParse(edadTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out edad) || edad <= 0)
            {
                errores.Add(new ErrorFilaImportacion(fila, "La edad debe ser un número entero mayor que cero."));
                valida = false;
            }
            if (string.IsNullOrWhiteSpace(vacuna))
            {
                errores.Add(new ErrorFilaImportacion(fila, "La vacuna es obligatoria."));
                valida = false;
            }

            if (valida)
            {
                var indice = items.FindIndex(i => i.EdadDia == edad);
                if (indice >= 0)
                {
                    var existente = items[indice];
                    items[indice] = existente with
                    {
                        Vacuna = Combinar(existente.Vacuna, vacuna.Trim())!,
                        ModoAplicacion = Combinar(existente.ModoAplicacion, TextoNulo(modo)),
                        Observaciones = Combinar(existente.Observaciones, TextoNulo(observaciones))
                    };
                }
                else
                {
                    items.Add(new ItemCronogramaImportado(
                        edad, vacuna.Trim(),
                        TextoNulo(modo),
                        TextoNulo(observaciones)));
                }
            }
            fila++;
        }

        if (items.Count == 0 && errores.Count == 0)
            errores.Add(new ErrorFilaImportacion(1, "El archivo no contiene filas de cronograma."));
        return new ResultadoImportacionCronograma(items, errores);
    }

    // La columna FECHA se ignora (spec SP7): se deriva de la entrada del lote
    // más la EdadDia al asignar.
    private static (int? Edad, int? Vacuna, int? ModoAplicacion, int? Observaciones) IndicesColumnas(IXLRow encabezado)
    {
        int? edad = null, vacuna = null, modo = null, observaciones = null;
        foreach (var celda in encabezado.CellsUsed())
        {
            var nombre = Normalizar(celda.GetString());
            if (nombre.StartsWith("EDAD", StringComparison.Ordinal))
                edad = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("VACUNA", StringComparison.Ordinal))
                vacuna = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("MODO", StringComparison.Ordinal))
                modo = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("OBSERVACIONES", StringComparison.Ordinal))
                observaciones = celda.Address.ColumnNumber;
        }
        return (edad, vacuna, modo, observaciones);
    }

    private static string TextoCelda(IXLWorksheet hoja, int fila, int? columna) =>
        columna is null ? string.Empty : hoja.Cell(fila, columna.Value).GetString().Trim();

    // Los ítems con la misma edad se fusionan en uno solo (spec SP7): se
    // concatenan VACUNA, MODO DE APLICACION y observaciones separadas por "; ",
    // sin duplicar segmentos idénticos y sin artefactos cuando un campo falta.
    private static string? Combinar(string? actual, string? adicional)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return adicional;
        if (string.IsNullOrWhiteSpace(adicional))
            return actual;
        if (string.Equals(actual, adicional, StringComparison.OrdinalIgnoreCase))
            return actual;
        return actual + "; " + adicional;
    }

    private static string? TextoNulo(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string Normalizar(string texto)
    {
        var constructor = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                constructor.Append(char.ToUpperInvariant(c));
        return string.Join(' ', constructor.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
