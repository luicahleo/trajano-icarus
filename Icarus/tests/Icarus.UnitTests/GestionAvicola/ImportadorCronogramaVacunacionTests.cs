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
    public void EdadRepetidaSeReporta()
    {
        using var excel = ExcelCon(EncabezadoCaisy,
        [
            ["", "3", "A", "", ""],
            ["", "3", "B", "", ""],
        ]);

        var resultado = new ImportadorCronogramaVacunacion().Importar(excel);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(3, error.Fila);
        Assert.Contains("repetida", error.Mensaje);
        Assert.Single(resultado.Items);
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
