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
        Assert.Equal(EstadoProgramaVacunacion.Borrador, programa.Estado);
        Assert.False(programa.EsAsignable);
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
    public void ReemplazarCronogramaGuardaLaFechaProgramadaDelItem()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([
            new DatosItemPlanVacunacion(3, "BIO COCCIVET R", null, null, new DateOnly(2026, 8, 8))]);
        Assert.Equal(new DateOnly(2026, 8, 8), programa.Items.Single().Fecha);
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
    public void ActualizarDatosModificaLosDatosBasicosSinTocarLaEmision()
    {
        var programa = CrearPrograma();
        programa.ActualizarDatos("PLAN NUEVO", 2000, "  Observación  ");
        Assert.Equal("PLAN NUEVO", programa.Nombre);
        Assert.Equal(Hoy.AddDays(-30), programa.FechaEmision);
        Assert.Equal(2000, programa.CantidadAves);
        Assert.Equal("Observación", programa.Observaciones);
    }

    [Fact]
    public void CtorSinFechaDejaLaEmisionEnNull()
    {
        var programa = new ProgramaVacunacion("PLAN", null, 1000, null);
        Assert.Null(programa.FechaEmision);
    }

    [Fact]
    public void EstablecerFechaEmisionValidaYRechazaFutura()
    {
        var programa = new ProgramaVacunacion("PLAN", null, 1000, null);
        programa.EstablecerFechaEmision(Hoy.AddDays(-2));
        Assert.Equal(Hoy.AddDays(-2), programa.FechaEmision);
        var ex = Assert.Throws<ReglaNegocioException>(() => programa.EstablecerFechaEmision(Hoy.AddDays(1)));
        Assert.Equal("La fecha de emisión no puede ser futura.", ex.Message);
    }

    private static ProgramaVacunacion ProgramaPublicado()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, "BIO COCCIVET R", null, null)]);
        programa.Publicar();
        return programa;
    }

    [Fact]
    public void PublicarConCronogramaLoVuelveAsignable()
    {
        var programa = ProgramaPublicado();

        Assert.Equal(EstadoProgramaVacunacion.Publicado, programa.Estado);
        Assert.True(programa.EsAsignable);
    }

    [Fact]
    public void PublicarSinCronogramaLanzaReglaNegocio()
    {
        var programa = CrearPrograma();

        var ex = Assert.Throws<ReglaNegocioException>(programa.Publicar);

        Assert.Equal("El programa debe tener un cronograma antes de publicarse.", ex.Message);
        Assert.Equal(EstadoProgramaVacunacion.Borrador, programa.Estado);
    }

    [Fact]
    public void PublicarDosVecesLanzaReglaNegocio()
    {
        var programa = ProgramaPublicado();

        var ex = Assert.Throws<ReglaNegocioException>(programa.Publicar);

        Assert.Equal("El programa ya está publicado.", ex.Message);
    }

    [Fact]
    public void PublicarUnBorradorEliminadoLanzaReglaNegocio()
    {
        var programa = CrearPrograma();
        programa.ReemplazarCronograma([new DatosItemPlanVacunacion(3, "A", null, null)]);
        programa.EliminarBorrador();

        var ex = Assert.Throws<ReglaNegocioException>(programa.Publicar);

        Assert.Equal("Un borrador eliminado no se puede publicar.", ex.Message);
    }

    [Fact]
    public void UnPublicadoEsInmutable()
    {
        var programa = ProgramaPublicado();

        Assert.Equal("Un programa publicado ya no es editable.",
            Assert.Throws<ReglaNegocioException>(() => programa.ActualizarDatos("OTRO", 10, null)).Message);
        Assert.Equal("Un programa publicado ya no es editable.",
            Assert.Throws<ReglaNegocioException>(() => programa.ReemplazarCronograma(
                [new DatosItemPlanVacunacion(7, "GUMBORO", null, null)])).Message);
        Assert.Equal("Un programa publicado ya no es editable.",
            Assert.Throws<ReglaNegocioException>(() => programa.EstablecerFechaEmision(Hoy)).Message);
        Assert.Equal("Un programa publicado ya no es editable.",
            Assert.Throws<ReglaNegocioException>(programa.AsegurarEditable).Message);
        Assert.Equal("PROGRAMA DE VACUNACION PARA 1000 AVES", programa.Nombre);
    }

    [Fact]
    public void EliminarBorradorLoDejaInactivoYEsInocuoRepetirlo()
    {
        var programa = CrearPrograma();

        programa.EliminarBorrador();
        programa.EliminarBorrador();

        Assert.False(programa.EstaActivo);
        Assert.Equal(EstadoProgramaVacunacion.Borrador, programa.Estado);
        Assert.False(programa.EsAsignable);
    }

    [Fact]
    public void EliminarUnPublicadoLanzaReglaNegocio()
    {
        var programa = ProgramaPublicado();

        var ex = Assert.Throws<ReglaNegocioException>(programa.EliminarBorrador);

        Assert.Equal("Un programa publicado no se elimina: se desactiva.", ex.Message);
        Assert.True(programa.EstaActivo);
    }

    [Fact]
    public void DesactivarUnPublicadoLoDejaInactivoSinBorrarYNoAsignable()
    {
        var programa = ProgramaPublicado();

        programa.Desactivar();
        programa.Desactivar();

        Assert.False(programa.EstaActivo);
        Assert.Equal(EstadoProgramaVacunacion.Publicado, programa.Estado);
        Assert.False(programa.EsAsignable);
        Assert.Single(programa.Items);
    }

    [Fact]
    public void DesactivarUnBorradorLanzaReglaNegocio()
    {
        var programa = CrearPrograma();

        var ex = Assert.Throws<ReglaNegocioException>(programa.Desactivar);

        Assert.Equal("Un borrador no se desactiva: se elimina.", ex.Message);
        Assert.True(programa.EstaActivo);
    }
}
