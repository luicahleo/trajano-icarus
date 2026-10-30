using System.Reflection;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Tests.Servicios;

// Decisión 7 del spec 2026-10-30: el gestor veterinario nunca ve
// asignaciones por galpón ni tareas. Ningún contrato que consume la
// aplicación de oficina para vacunación expone datos del tenant.
public class ContratosVacunacionSinTenantTests
{
    private static readonly string[] PalabrasProhibidas =
        ["Galpon", "Cliente", "Tarea", "Trabajador", "Granja", "Autor"];

    [Theory]
    [InlineData(typeof(ProgramaVacunacionGestionResumenApi))]
    [InlineData(typeof(ProgramaVacunacionGestionDetalleApi))]
    [InlineData(typeof(ItemProgramaVacunacionApi))]
    public void LosContratosDeVacunacionNoExponenDatosDelTenant(Type tipo)
    {
        foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(
                PalabrasProhibidas,
                palabra => propiedad.Name.Contains(palabra, StringComparison.OrdinalIgnoreCase));
        }
    }
}
