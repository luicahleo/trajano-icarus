using Icarus.GestionAvicola.Domain;

namespace Icarus.UnitTests.GestionAvicola;

public sealed class CatalogoAlimentosCaisyTests
{
    [Theory]
    [InlineData("SJ-PRE", TipoAlimento.Preiniciador, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-PREG", TipoAlimento.Preiniciador, PresentacionAlimento.Granel)]
    [InlineData("SJ-1B", TipoAlimento.Iniciador, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-1G", TipoAlimento.Iniciador, PresentacionAlimento.Granel)]
    [InlineData("SJ-2B", TipoAlimento.Crecimiento, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-2G", TipoAlimento.Crecimiento, PresentacionAlimento.Granel)]
    [InlineData("SJ-3B", TipoAlimento.Finalizador, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-3G", TipoAlimento.Finalizador, PresentacionAlimento.Granel)]
    [InlineData("SJ-P1B", TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-P1G", TipoAlimento.PosturaUno, PresentacionAlimento.Granel)]
    [InlineData("SJ-P2B", TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa)]
    [InlineData("SJ-P2G", TipoAlimento.PosturaDos, PresentacionAlimento.Granel)]
    public void CodigoTieneIdaYVuelta(string codigo, TipoAlimento tipo, PresentacionAlimento presentacion)
    {
        var producto = CatalogoAlimentosCaisy.BuscarPorCodigo(codigo);

        Assert.NotNull(producto);
        Assert.Equal((tipo, presentacion), (producto.Value.Tipo, producto.Value.Presentacion));
        Assert.Equal(codigo, CatalogoAlimentosCaisy.CodigoDe(tipo, presentacion));
    }

    [Fact]
    public void CodigoDesconocidoNoSeEncuentra()
    {
        Assert.Null(CatalogoAlimentosCaisy.BuscarPorCodigo("XX-99"));
    }
}
