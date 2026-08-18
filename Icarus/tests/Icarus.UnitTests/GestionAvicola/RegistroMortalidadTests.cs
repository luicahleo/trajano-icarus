using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistroMortalidadTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static RegistroMortalidad Crear(DateOnly? fecha = null) => new(Guid.NewGuid(), Guid.NewGuid(), fecha ?? Hoy, new(6, 0), 15, 4785, null);
    [Fact] public void CtorValidoAsignaYNaceActivo() { var r = Crear(); Assert.Equal(15, r.CantidadMuertas); Assert.Equal(4785, r.GallinasVivas); Assert.True(r.EstaActivo); }
    [Fact] public void CtorSinMuertasLanzaReglaNegocio() { var ex = Assert.Throws<ReglaNegocioException>(() => new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy, default, 0, 4800, null)); Assert.Equal("La cantidad de muertas debe ser mayor que cero.", ex.Message); }
    [Fact] public void CtorFechaFuturaLanzaReglaNegocio() => Assert.Throws<ReglaNegocioException>(() => Crear(Hoy.AddDays(1)));
    [Fact] public void EditarDeHoyActualizaCantidadHoraYSnapshot() { var r = Crear(); r.Editar(20, new(7, 0), 4780); Assert.Equal(20, r.CantidadMuertas); Assert.Equal(new TimeOnly(7, 0), r.Hora); Assert.Equal(4780, r.GallinasVivas); }
    [Fact] public void EditarDeAyerLanzaSellado() { var ex = Assert.Throws<ReglaNegocioException>(() => Crear(Hoy.AddDays(-1)).Editar(20, default, 4780)); Assert.Equal("El registro está sellado: solo se puede corregir el mismo día.", ex.Message); }
    [Fact] public void DesactivarDeAyerLanzaSellado() { var r = Crear(Hoy.AddDays(-1)); Assert.Throws<ReglaNegocioException>(r.Desactivar); Assert.True(r.EstaActivo); }
    [Fact] public void DesactivarDeHoyMarcaInactivoSinBorrar() { var r = Crear(); r.Desactivar(); Assert.False(r.EstaActivo); }
}
