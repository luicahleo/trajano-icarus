using System.Security.Claims;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Autenticacion;

public class PrincipalGestorcaisyTests
{
    [Fact]
    public void CreaUnPrincipalAutenticadoConLosClaimsDeSesion()
    {
        var token = CreadorTokens.Crear(rol: "GestorCaisy", funcCaisy: 1);
        var refresh = "refresh-de-prueba";

        var principal = PrincipalGestorcaisy.Crear(token, refresh);

        Assert.NotNull(principal);
        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("GestorCaisy", principal.FindFirst(ConstantesAutorizacion.ClaimRol)?.Value);
        Assert.Equal("1", principal.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value);
        Assert.Equal(token, principal.FindFirst(ConstantesAutorizacion.ClaimAccessToken)?.Value);
        Assert.Equal(refresh, principal.FindFirst(ConstantesAutorizacion.ClaimRefreshToken)?.Value);
    }

    [Fact]
    public void TokenIlegibleDevuelveNulo()
    {
        Assert.Null(PrincipalGestorcaisy.Crear("token-roto", "refresh"));
    }

    [Fact]
    public void AlRenovarReemplazaLosTokensYConservaElResto()
    {
        var token = CreadorTokens.Crear(rol: "GestorCaisy", funcCaisy: 1);
        var original = PrincipalGestorcaisy.Crear(token, "refresh-viejo")!;

        var renovado = PrincipalGestorcaisy.ConTokensRenovados(
            original, "access-nuevo", "refresh-nuevo");

        Assert.Equal("access-nuevo", renovado.FindFirst(ConstantesAutorizacion.ClaimAccessToken)?.Value);
        Assert.Equal("refresh-nuevo", renovado.FindFirst(ConstantesAutorizacion.ClaimRefreshToken)?.Value);
        Assert.Equal("GestorCaisy", renovado.FindFirst(ConstantesAutorizacion.ClaimRol)?.Value);
        Assert.Equal("1", renovado.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value);
        Assert.NotEqual(token, renovado.FindFirst(ConstantesAutorizacion.ClaimAccessToken)?.Value);
    }

    [Fact]
    public void ElCorreoQueEscribeElUsuarioQuedaComoClaim()
    {
        var token = CreadorTokens.Crear();
        var sinCorreo = PrincipalGestorcaisy.Crear(token, "refresh")!;

        var conCorreo = PrincipalGestorcaisy.ConCorreo(sinCorreo, "gestor@caisy.test");

        Assert.Equal("gestor@caisy.test", conCorreo.FindFirst(ConstantesAutorizacion.ClaimCorreo)?.Value);
        Assert.Null(sinCorreo.FindFirst(ConstantesAutorizacion.ClaimCorreo)?.Value);
    }
}
