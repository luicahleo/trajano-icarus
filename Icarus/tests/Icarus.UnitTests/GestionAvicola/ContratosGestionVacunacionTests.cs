using System.Reflection;
using Icarus.GestionAvicola.Application.Vacunacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// Decisión 7 del spec 2026-10-30: el gestor veterinario ve el catálogo
// completo pero nunca asignaciones por galpón ni tareas. Este test vuelve
// ejecutable la decisión: ningún DTO de gestión expone datos del tenant.
public class ContratosGestionVacunacionTests
{
    private static readonly string[] PalabrasProhibidas =
        ["Galpon", "Cliente", "Tarea", "Trabajador", "Granja", "Asignacion"];

    [Theory]
    [InlineData(typeof(ProgramaVacunacionGestionResumen))]
    [InlineData(typeof(ProgramaVacunacionGestionDetalle))]
    [InlineData(typeof(ItemPlanVacunacionResumen))]
    public void LosContratosDelGestorNoExponenDatosDelTenant(Type tipo)
    {
        foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(
                PalabrasProhibidas,
                palabra => propiedad.Name.Contains(palabra, StringComparison.OrdinalIgnoreCase));
        }
    }
}
