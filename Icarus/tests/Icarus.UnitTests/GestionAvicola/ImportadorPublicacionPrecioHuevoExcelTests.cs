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
}
