using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ProgramaVacunacionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private static ProgramaVacunacion CrearPrograma() =>
        new("PROGRAMA DE VACUNACION PARA 1000 AVES", Hoy.AddDays(-30), 1000, null);

    [Fact]
    public void CtorValidoAsignaYNaceActivo()
    {
        var programa = CrearPrograma();
        Assert.Equal("PROGRAMA DE VACUNACION PARA 1000 AVES", programa.Nombre);
        Assert.Equal(Hoy.AddDays(-30), programa.FechaEmision);
        Assert.Equal(1000, programa.CantidadAves);
        Assert.True(programa.EstaActivo);
        Assert.Empty(programa.Items);
    }

    [Fact]
    public void CtorNombreVacioLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("  ", Hoy, 1000, null));
        Assert.Equal("El nombre del programa es obligatorio.", ex.Message);
    }

    [Fact]
    public void CtorFechaEmisionFuturaLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("Plan", Hoy.AddDays(1), 1000, null));
        Assert.Equal("La fecha de emisión no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CtorCantidadAvesInvalidaLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new ProgramaVacunacion("Plan", Hoy, 0, null));
        Assert.Equal("La cantidad de aves debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void ReemplazarCronogramaCreaItemsActivos()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
            new DatosItemPlanVacunacion(10, "NEWCASTLE + BRONQUITIS", "Gota ocular", "Ayuno de agua 2 horas"),
        ]);
        Assert.Equal(2, programa.Items.Count);
        Assert.All(programa.Items, i => Assert.True(i.EstaActivo));
        var primero = programa.Items.Single(i => i.EdadDia == 3);
        Assert.Equal("BIO COCCIVET R", primero.Vacuna);
        Assert.Equal("Agua de bebida", primero.ModoAplicacion);
        Assert.NotEqual(Guid.Empty, primero.Id);
    }

    [Fact]
    public void ReemplazarCronogramaDesactivaLosAnterioresSinBorrarlos()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, "BIO COCCIVET R", null, null)]);
        var anterior = programa.Items.Single();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(7, "GUMBORO", null, null)]);
        Assert.False(anterior.EstaActivo);
        Assert.Equal(2, programa.Items.Count);
        Assert.Single(programa.Items, i => i.EstaActivo);
    }

    [Fact]
    public void ReemplazarCronogramaConEdadDuplicadaLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() => programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "A", null, null),
            new DatosItemPlanVacunacion(3, "B", null, null)]));
        Assert.Equal("El cronograma no puede repetir la edad en días entre ítems.", ex.Message);
    }

    [Fact]
    public void ReemplazarCronogramaVacioLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() => programa.ReemplazarCronograma([]));
        Assert.Equal("El cronograma debe tener al menos un ítem.", ex.Message);
    }

    [Fact]
    public void ItemSinEdadLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            programa.ReemplazarCronograma([new DatosItemPlanVacunacion(0, "A", null, null)]));
        Assert.Equal("La edad en días debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void ItemSinVacunaLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, " ", null, null)]));
        Assert.Equal("La vacuna del ítem es obligatoria.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosModificaLosDatosBasicos()
    {
        var programa = CrearPrograma();
        programa.ActualizarDatos("PLAN NUEVO", Hoy.AddDays(-5), 2000, "  Observación  ");
        Assert.Equal("PLAN NUEVO", programa.Nombre);
        Assert.Equal(Hoy.AddDays(-5), programa.FechaEmision);
        Assert.Equal(2000, programa.CantidadAves);
        Assert.Equal("Observación", programa.Observaciones);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var programa = CrearPrograma();
        programa.Desactivar();
        Assert.False(programa.EstaActivo);
    }
}
