using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;

namespace Icarus.UnitTests.GestionAvicola;

public class GranjaTests
{
    [Fact]
    public void CtorSinClienteLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new Granja(Guid.Empty, "Granja Norte"));
        Assert.Equal("La granja debe pertenecer a un cliente.", ex.Message);
    }

    [Fact]
    public void CtorNombreVacioLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new Granja(Guid.NewGuid(), "  "));
        Assert.Equal("El nombre de la granja es obligatorio.", ex.Message);
    }

    [Fact]
    public void CtorValidoRecortaNombreYNaceActiva()
    {
        var granja = new Granja(Guid.NewGuid(), "  Granja Norte  ");
        Assert.Equal("Granja Norte", granja.Nombre);
        Assert.True(granja.EstaActivo);
    }

    [Fact]
    public void RenombrarVacioLanzaReglaNegocio()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        Assert.Throws<ReglaNegocioException>(() => granja.Renombrar(""));
    }

    [Fact]
    public void RenombrarValidoRecorta()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        granja.Renombrar("  Granja Sur ");
        Assert.Equal("Granja Sur", granja.Nombre);
    }

    [Fact]
    public void DesactivarMarcaInactivaSinBorrar()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        granja.Desactivar();
        Assert.False(granja.EstaActivo);
    }
}
