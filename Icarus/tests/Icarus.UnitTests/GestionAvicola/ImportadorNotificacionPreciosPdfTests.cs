using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Importacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8A Tarea 3 (spec: "Importación del PDF"): el importador extrae una
// propuesta determinista de cabecera, aportes y detalles desde una copia
// anonimizada del documento real; un formato no interpretable devuelve la
// lista de errores sin propuesta parcial.
public class ImportadorNotificacionPreciosPdfTests
{
    private static FileStream AbrirFixture()
    {
        var exe = AppContext.BaseDirectory;
        return File.OpenRead(Path.Combine(exe, "GestionAvicola", "Fixtures", "NotificacionPreciosMuestra.pdf"));
    }

    [Fact]
    public void ExtraeLaCabeceraConFecha20251102YAportes()
    {
        using var fixture = AbrirFixture();

        var resultado = new ImportadorNotificacionPreciosPdf().Importar(fixture);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Propuesta);
        Assert.Equal(new DateOnly(2025, 11, 2), resultado.Propuesta!.FechaDocumento);
        Assert.Equal(new DateOnly(2025, 11, 10), resultado.Propuesta.VigenteDesde);
        Assert.Equal(1.20m, resultado.Propuesta.AporteCaisy);
        Assert.Equal(0.60m, resultado.Propuesta.Fondo);
        Assert.Equal(0.75m, resultado.Propuesta.Servicios);
    }

    [Fact]
    public void ExtraeDoceFilasConEdadesYPreciosNuevos()
    {
        using var fixture = AbrirFixture();

        var resultado = new ImportadorNotificacionPreciosPdf().Importar(fixture);

        var propuesta = resultado.Propuesta!;
        Assert.Empty(resultado.Errores);
        Assert.Equal(12, propuesta.Detalles.Count);

        var iniciadorBolsa = propuesta.Detalles.Single(d =>
            d.TipoAlimento == TipoAlimento.Iniciador && d.Presentacion == PresentacionAlimento.Bolsa);
        Assert.Equal(176.50m, iniciadorBolsa.PrecioFinalPor40Kg);
        Assert.Equal(175.00m, iniciadorBolsa.PrecioActualDocumento);
        Assert.Equal(22, iniciadorBolsa.EdadDesdeDias);
        Assert.Equal(35, iniciadorBolsa.EdadHastaDias);

        var posturaDosGranel = propuesta.Detalles.Single(d =>
            d.TipoAlimento == TipoAlimento.PosturaDos && d.Presentacion == PresentacionAlimento.Granel);
        Assert.Equal(182.50m, posturaDosGranel.PrecioFinalPor40Kg);
        Assert.Equal(181.00m, posturaDosGranel.PrecioActualDocumento);
        Assert.Equal(76, posturaDosGranel.EdadDesdeDias);
        Assert.Equal(90, posturaDosGranel.EdadHastaDias);
    }

    [Fact]
    public void UnFormatoNoInterpretableDevuelveErroresSinPropuestaParcial()
    {
        using var contenido = new MemoryStream("no es un pdf"u8.ToArray());

        var resultado = new ImportadorNotificacionPreciosPdf().Importar(contenido);

        Assert.NotEmpty(resultado.Errores);
        Assert.Null(resultado.Propuesta);
    }

    [Fact]
    public void UnPdfSinFilasValidasDevuelveErroresSinPropuesta()
    {
        using var fixture = AbrirFixture();
        var bytes = new byte[fixture.Length];
        fixture.ReadExactly(bytes);
        var texto = System.Text.Encoding.ASCII.GetString(bytes);
        // Sin filas de precio: los códigos dejan de empezar por SJ- (mismo
        // largo, para no invalidar el /Length del flujo de contenido).
        texto = texto.Replace("SJ-", "XX-");
        using var contenido = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(texto));

        var resultado = new ImportadorNotificacionPreciosPdf().Importar(contenido);

        Assert.Contains(resultado.Errores, e => e.Mensaje.Contains("filas de precio", StringComparison.Ordinal));
        Assert.Null(resultado.Propuesta);
    }
}
