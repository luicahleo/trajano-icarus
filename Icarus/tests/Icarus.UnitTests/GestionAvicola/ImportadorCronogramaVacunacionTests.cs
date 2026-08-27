using ClosedXML.Excel;
using Icarus.GestionAvicola.Infrastructure.Importacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ImportadorCronogramaVacunacionTests
{
    private static readonly string[] EncabezadoCaisy =
        ["FECHA", "EDAD", "VACUNA", "MODO DE APLICACION", "OBSERVACIONES"];

    private static MemoryStream ExcelCon(string[] encabezados, string[][] filas)
    {
        var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        for (var c = 0; c < encabezados.Length; c++)
            hoja.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = filas[f][c];
        var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        memoria.Position = 0;
        return memoria;
    }

    [Fact]
    public void ExcelValidoDevuelveItemsIgnorandoLaColumnaFecha()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["09/10/2023", "3", "BIO COCCIVET R", "Agua de bebida", ""],
            ["16/10/2023", "10", "NEWCASTLE + BRONQUITIS", "Gota ocular", "Ayuno 2 horas"],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal(3, resultado.Items[0].EdadDia);
        Assert.Equal("BIO COCCIVET R", resultado.Items[0].Vacuna);
        Assert.Equal("Agua de bebida", resultado.Items[0].ModoAplicacion);
        Assert.Null(resultado.Items[0].Observaciones);
        Assert.Equal("Ayuno 2 horas", resultado.Items[1].Observaciones);
    }

    [Fact]
    public void EncabezadosConTildesMinusculasYEspaciosSeReconocen()
    {
        using var excel = ExcelCon(
            ["  fecha ", "edad día", "vacuna", "  modo   de aplicación ", "observaciones"],
            [["", "5", "GUMBORO", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        Assert.Single(resultado.Items);
        Assert.Equal(5, resultado.Items[0].EdadDia);
    }

    [Fact]
    public void FilaSinEdadSeReportaPorNumeroDeFila()
    {
        using var excel = ExcelCon(EncabezadoCaisy, [["", "", "GUMBORO", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(2, error.Fila);
        Assert.Contains("edad", error.Mensaje);
        Assert.Empty(resultado.Items);
    }

    [Fact]
    public void EdadRepetidaSeFusionaEnUnSoloItem()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["", "3", "A", "Agua de bebida", "Nota 1"],
            ["", "3", "B", "Inyección", "Nota 2"],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        var item = Assert.Single(resultado.Items);
        Assert.Equal(3, item.EdadDia);
        Assert.Equal("A; B", item.Vacuna);
        Assert.Equal("Agua de bebida; Inyección", item.ModoAplicacion);
        Assert.Equal("Nota 1; Nota 2", item.Observaciones);
    }

    [Fact]
    public void EdadRepetidaFusionaSinArtefactosCuandoFaltaUnCampo()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["", "10", "A", "", ""],
            ["", "10", "B", "Gota ocular", ""],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        var item = Assert.Single(resultado.Items);
        Assert.Equal("A; B", item.Vacuna);
        Assert.Equal("Gota ocular", item.ModoAplicacion);
        Assert.Null(item.Observaciones);
    }

    [Fact]
    public void EdadRepetidaNoDuplicaTextoIdentico()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["", "60", "INFLUENZA AVIAR", "Intramuscular", ""],
            ["", "60", "INFLUENZA AVIAR", "Intramuscular", ""],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Empty(resultado.Errores);
        var item = Assert.Single(resultado.Items);
        Assert.Equal("INFLUENZA AVIAR", item.Vacuna);
        Assert.Equal("Intramuscular", item.ModoAplicacion);
    }

    [Fact]
    public void FilaSinVacunaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy, [["", "7", " ", "", ""]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(2, error.Fila);
        Assert.Contains("vacuna", error.Mensaje);
    }

    [Fact]
    public void EncabezadoSinColumnasRequeridasSeReporta()
    {
        using var excel = ExcelCon(["FECHA", "OBSERVACIONES"], [["x", "y"]]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(1, error.Fila);
        Assert.Empty(resultado.Items);
    }

    [Fact]
    public void HojaSinFilasDeCronogramaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy, []);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        Assert.Single(resultado.Errores);
        Assert.Empty(resultado.Items);
    }
}
