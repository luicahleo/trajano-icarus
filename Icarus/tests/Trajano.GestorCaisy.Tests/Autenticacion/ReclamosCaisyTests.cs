using System.Security.Claims;
using Trajano.GestorCaisy.Autenticacion;

namespace Trajano.GestorCaisy.Tests.Autenticacion;

public class ReclamosCaisyTests
{
    private static ClaimsPrincipal Usuario(string rol, int? funcCaisy)
    {
        var claims = new List<Claim> { new("rol", rol) };
        if (funcCaisy is { } valor)
            claims.Add(new Claim("funcCaisy", valor.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba"));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(7, true)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void TieneGestorVeterinarioSegunElBitCuatro(int funcCaisy, bool esperado)
    {
        Assert.Equal(esperado, ReclamosCaisy.TieneGestorVeterinario(Usuario("GestorCaisy", funcCaisy)));
    }

    [Fact]
    public void UnRolAjenoNoTieneGestorVeterinarioAunqueTraigaElBit()
    {
        Assert.False(ReclamosCaisy.TieneGestorVeterinario(Usuario("Cliente", 4)));
    }

    [Theory]
    [InlineData(1, "Precios")]
    [InlineData(2, "PreciosHuevo")]
    [InlineData(4, "ProgramasVacunacion")]
    [InlineData(6, "PreciosHuevo")]
    [InlineData(0, "Precios")]
    public void ControladorInicialEsLaPrimeraSeccionDisponible(int funcCaisy, string esperado)
    {
        Assert.Equal(esperado, ReclamosCaisy.ControladorInicial(Usuario("GestorCaisy", funcCaisy)));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    public void TieneAlgunaFuncionalidadIncluyeLaVeterinaria(int funcCaisy, bool esperado)
    {
        Assert.Equal(esperado, ReclamosCaisy.TieneAlgunaFuncionalidad(Usuario("GestorCaisy", funcCaisy)));
    }
}
