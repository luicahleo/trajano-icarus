using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EficienciaPosturaTests
{
    [Fact]
    public void MapleSonTreintaHuevos() => Assert.Equal(30, Maple.HuevosPorMaple);

    [Fact]
    public void UmbralDeDescarteEsSetenta() => Assert.Equal(70m, EficienciaPostura.UmbralDescarte);

    [Fact]
    public void CalcularDevuelvePorcentajeConDosDecimales() =>
        Assert.Equal(80.81m, EficienciaPostura.Calcular(2400, 2970));

    [Fact]
    public void CalcularSinGallinasDevuelveCero() =>
        Assert.Equal(0m, EficienciaPostura.Calcular(2400, 0));

    [Fact]
    public void EstaBajoUmbralComparaContraElSetenta()
    {
        Assert.True(EficienciaPostura.EstaBajoUmbral(69.99m));
        Assert.False(EficienciaPostura.EstaBajoUmbral(70m));
    }
}
