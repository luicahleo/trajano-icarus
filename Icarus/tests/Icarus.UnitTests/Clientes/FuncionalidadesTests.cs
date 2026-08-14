using Icarus.Clientes.Domain;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class FuncionalidadesTests
{
    public static IEnumerable<object[]> TodasMenosNinguno()
    {
        foreach (var funcionalidad in Enum.GetValues<Funcionalidades>())
        {
            if (funcionalidad == Funcionalidades.Ninguno)
                continue;
            yield return new object[] { funcionalidad };
        }
    }

    [Theory]
    [MemberData(nameof(TodasMenosNinguno))]
    public void TodasLasFuncionalidadesPertenecenAGestionAvicola(Funcionalidades funcionalidad)
    {
        Assert.Equal(Modulos.GestionAvicola, FuncionalidadesModulos.ModuloDe(funcionalidad));
    }

    [Fact]
    public void ControlAccesoNoTieneFuncionalidades()
    {
        Assert.Equal(Funcionalidades.Ninguno, FuncionalidadesModulos.FuncionalidadesDelModulo(Modulos.ControlAcceso));
    }

    [Fact]
    public void NingunoNoPerteneceANingunModulo()
    {
        Assert.Equal(Modulos.Ninguno, FuncionalidadesModulos.ModuloDe(Funcionalidades.Ninguno));
    }

    [Fact]
    public void LosValoresNumericosSonEstables()
    {
        var valores = Enum.GetValues<Funcionalidades>()
            .Where(f => f != Funcionalidades.Ninguno)
            .Select(f => (int)f)
            .OrderBy(v => v)
            .ToArray();

        Assert.Equal(8, valores.Length);
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 32, 64, 128 }, valores);
    }
}
