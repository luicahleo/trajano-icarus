using ClosedXML.Excel;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Importacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ImportadorPublicacionPrecioHuevoExcelTests
{
    private static Stream AbrirFixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory,
            "GestionAvicola", "Fixtures", "PublicacionPrecioHuevoMuestra.xlsx"));

    [Fact]
    public void ImportaLasSeisFilasConFechasYServicioComun()
    {
        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(AbrirFixture());

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Propuesta);
        Assert.Equal(new DateOnly(2026, 10, 30), resultado.Propuesta!.FechaNotificacion);
        Assert.Equal(new DateOnly(2026, 11, 1), resultado.Propuesta.FechaVigencia);
        Assert.Equal(0.057m, resultado.Propuesta.Servicio);
        Assert.Equal(6, resultado.Propuesta.Detalles.Count);
        Assert.Contains(resultado.Propuesta.Detalles, d => d.Tamano == TamanoHuevo.Extra);
    }

    [Fact]
    public void UnArchivoSinCabeceraDeTablaProduceError()
    {
        using var vacio = new MemoryStream();
        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(vacio);

        Assert.NotEmpty(resultado.Errores);
        Assert.Null(resultado.Propuesta);
    }

    [Fact]
    public void UnPrecioAlProductorCeroDetallaFilaColumnaYValor()
    {
        using var contenido = CrearExcel(precioProductor: 0);

        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(contenido);

        var error = Assert.Single(resultado.Errores, e => e.Columna == "NUEVO PRECIO AL PRODUCTOR");
        Assert.Equal(7, error.Fila);
        Assert.Equal("0", error.Valor);
    }

    [Fact]
    public void UnTamanoDesconocidoDetallaFilaColumnaYValor()
    {
        using var contenido = CrearExcel(tamano: "Mediano XL");

        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(contenido);

        var error = Assert.Single(resultado.Errores, e => e.Columna == "TAMAÑO");
        Assert.Equal(7, error.Fila);
        Assert.Equal("Mediano XL", error.Valor);
    }

    [Fact]
    public void LosErroresDeArchivoNoInventanUbicacion()
    {
        using var contenido = new MemoryStream();

        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(contenido);

        Assert.NotEmpty(resultado.Errores);
        Assert.All(resultado.Errores, e =>
        {
            Assert.Null(e.Columna);
            Assert.Null(e.Valor);
        });
    }

    private static MemoryStream CrearExcel(
        string tamano = "EXTRA", double precioProductor = 0.7957,
        double servicio = 0.057, double precioActual = 0.7954)
    {
        var stream = new MemoryStream();
        using (var libro = new XLWorkbook())
        {
            var hoja = libro.AddWorksheet("Precios");
            hoja.Cell("A1").Value = "CAMBIO DE PRECIO DE HUEVO";
            hoja.Cell("A3").Value = "FECHA DE NOTIFICACION:"; hoja.Cell("B3").Value = "30/10/2026";
            hoja.Cell("A4").Value = "FECHA DE ENTRADA EN VIGENCIA:"; hoja.Cell("B4").Value = "01/11/2026";
            hoja.Cell("A6").Value = "TAMAÑO";
            hoja.Cell("B6").Value = "PRECIO ACTUAL";
            hoja.Cell("C6").Value = "NUEVO PRECIO AL PRODUCTOR";
            hoja.Cell("D6").Value = "SERVICIOS";
            hoja.Cell("A7").Value = tamano;
            hoja.Cell("B7").Value = precioActual;
            hoja.Cell("C7").Value = precioProductor;
            hoja.Cell("D7").Value = servicio;
            libro.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }
}
