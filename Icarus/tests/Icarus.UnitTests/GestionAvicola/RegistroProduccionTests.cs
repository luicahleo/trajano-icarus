using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistroProduccionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static RegistroProduccion Crear(DateOnly? fecha = null) => new(Guid.NewGuid(), Guid.NewGuid(), fecha ?? Hoy, new(10, 0), 10, 5, 1, 2, 4800, null);

    [Fact] public void CtorValidoAsignaYNaceActivo() { var r = Crear(); Assert.Equal(10, r.CantidadMaples); Assert.Equal(5, r.UnidadesIncompletas); Assert.Equal(1, r.MaplesDescarte); Assert.Equal(2, r.UnidadesDescarte); Assert.Equal(4800, r.GallinasVivas); Assert.True(r.EstaActivo); }
    [Fact] public void TotalesUsanLaConstanteDelMaple() { var r = Crear(); Assert.Equal(305, r.TotalHuevosVendibles()); Assert.Equal(32, r.TotalHuevosDescarte()); }
    [Fact] public void CtorFechaFuturaLanzaReglaNegocio() => Assert.Throws<ReglaNegocioException>(() => Crear(Hoy.AddDays(1)));
    [Fact] public void CtorSueltosInvalidosLanzaReglaNegocio() { Assert.Throws<ReglaNegocioException>(() => new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy, default, 1, 30, 0, 0, 100, null)); Assert.Throws<ReglaNegocioException>(() => new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy, default, 1, 0, 0, 30, 100, null)); }
    [Fact] public void EditarDeHoyActualizaCantidadesYHora() { var r = Crear(); r.Editar(12, 0, 2, 0, new(14, 30)); Assert.Equal(12, r.CantidadMaples); Assert.Equal(new TimeOnly(14, 30), r.Hora); }
    [Fact] public void EditarDeAyerLanzaSellado() { var ex = Assert.Throws<ReglaNegocioException>(() => Crear(Hoy.AddDays(-1)).Editar(12, 0, 0, 0, default)); Assert.Equal("El registro está sellado: solo se puede corregir el mismo día.", ex.Message); }
    [Fact] public void DesactivarDeAyerLanzaSellado() { var r = Crear(Hoy.AddDays(-1)); Assert.Throws<ReglaNegocioException>(r.Desactivar); Assert.True(r.EstaActivo); }
    [Fact] public void DesactivarDeHoyMarcaInactivoSinBorrar() { var r = Crear(); r.Desactivar(); Assert.False(r.EstaActivo); }
}
