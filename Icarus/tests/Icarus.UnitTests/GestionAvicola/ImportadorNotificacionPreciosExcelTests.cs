using ClosedXML.Excel;
using Icarus.GestionAvicola.Infrastructure.Importacion;

namespace Icarus.UnitTests.GestionAvicola;

public sealed class ImportadorNotificacionPreciosExcelTests
{
    [Fact]
    public void ExcelValidoDevuelveCabeceraYDetalles()
    {
        using var contenido = CrearExcel();

        var resultado = new ImportadorNotificacionPreciosExcel().Importar(contenido);

        Assert.Empty(resultado.Errores);
        var propuesta = Assert.IsType<Icarus.GestionAvicola.Application.PreciosAlimentos.DatosNotificacionPdf>(resultado.Propuesta);
        Assert.Equal(new DateOnly(2026, 9, 5), propuesta.VigenteDesde);
        Assert.Equal(180m, propuesta.Detalles.Single().PrecioFinalPor40Kg);
    }

    [Fact]
    public void ExcelSinPrecioNuevoRechazaLaImportacionCompleta()
    {
        using var contenido = CrearExcel(cambiarPrecio: true);

        var resultado = new ImportadorNotificacionPreciosExcel().Importar(contenido);

        Assert.Null(resultado.Propuesta);
        Assert.Contains(resultado.Errores, e => e.Mensaje.Contains("precio nuevo", StringComparison.OrdinalIgnoreCase));
    }

    private static MemoryStream CrearExcel(bool cambiarPrecio = false)
    {
        var stream = new MemoryStream();
        using (var libro = new XLWorkbook())
        {
            var hoja = libro.AddWorksheet("Precios");
            hoja.Cell("A1").Value = "CAMBIO DE PRECIO DE ALIMENTO BALANCEADO";
            hoja.Cell("A4").Value = "Fecha de Notificación:"; hoja.Cell("B4").Value = "05/09/2026";
            hoja.Cell("A5").Value = "Fecha de Aplicación:"; hoja.Cell("B5").Value = "05/09/2026";
            hoja.Cell("A7").Value = "Código"; hoja.Cell("B7").Value = "Tipo / Descripción de Alimento";
            hoja.Cell("C7").Value = "Presentación"; hoja.Cell("D7").Value = "Edad de Alimentación (Días)";
            hoja.Cell("E7").Value = "Precio Actual (40kg) [Bs.]"; hoja.Cell("F7").Value = "Precio Nuevo (40kg) [Bs.]";
            hoja.Cell("G7").Value = "Diferencia [Bs.]";
            hoja.Cell("A8").Value = "SJ-1B"; hoja.Cell("B8").Value = "Crecimiento";
            hoja.Cell("C8").Value = "Saco / Bolsa"; hoja.Cell("D8").Value = "29 - 70";
            hoja.Cell("E8").Value = 170; hoja.Cell("F8").Value = cambiarPrecio ? "" : 180;
            hoja.Cell("A10").Value = "Nota: Los precios incluyen Reserva de Utilización (Bs. 1.20), Aporte/Cuota (Bs. 0.60) y Comisión de Procesamiento (Bs. 0.75).";
            libro.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }
}
