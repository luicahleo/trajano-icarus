using Icarus.BuildingBlocks.Application;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

// Contrato único de paginación (spec 2026-09-14): normaliza acá, una vez, en
// vez de repetir Math.Max en cada handler como se venía haciendo.
public class PaginacionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void LaPaginaNuncaEsMenorQueUno(int pedida, int esperada) =>
        Assert.Equal(esperada, new PeticionPaginada(pedida, 20).PaginaNormalizada);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(500, 100)]
    [InlineData(50, 50)]
    public void ElTamanoSeAcotaEntreUnoYCien(int pedido, int esperado) =>
        Assert.Equal(esperado, new PeticionPaginada(1, pedido).TamanoNormalizado);

    [Fact]
    public void ElSaltoDependeDeLaPaginaNormalizadaNoDeLaCruda()
    {
        // Una página 0 no debe producir un salto negativo, que en SQL Server
        // es un error de ejecución, no un cero silencioso.
        Assert.Equal(0, new PeticionPaginada(0, 20).Salto);
        Assert.Equal(40, new PeticionPaginada(3, 20).Salto);
    }

    [Fact]
    public void UnaPaginaVaciaSigueInformandoElTotal()
    {
        var pagina = new Pagina<string>([], 137, 9, 20);
        Assert.Empty(pagina.Items);
        Assert.Equal(137, pagina.Total);
    }
}
