using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Icarus.UnitTests.Identity;

// SP8A Tarea 1 (spec: "Aplicaciones y autorización"): GestorCaisy es un rol
// global sin tenant con funcionalidades componibles (FuncionalidadesCaisy); el
// Administrador de plataforma crea, desactiva y asigna funciones a las cuentas.
public class FuncionalidadesCaisyTests
{
    private static EmisorAccessTokens CrearEmisor() =>
        new(Options.Create(new OpcionesJwt
        {
            Clave = new string('k', 32),
            Emisor = "Icarus",
            Audiencia = "Icarus",
            MinutosAccessToken = 15,
        }));

    private static JsonWebToken Leer(string token) =>
        new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public void GestorCaisyEsRolGlobalSinTenant()
    {
        Assert.False(ReglasRol.RequiereCliente(Rol.GestorCaisy));
        Assert.False(ReglasRol.RequiereCliente(nameof(Rol.GestorCaisy)));
    }

    [Theory]
    [InlineData("GestorPedidoAlimento")]
    public void SoloSeAdmitenFuncionalidadesDefinidas(string nombre)
    {
        Assert.True(ReglasFuncionalidadesCaisy.EsValida(nombre));
    }

    [Theory]
    [InlineData("GestorRecepcionHuevos")]
    [InlineData("")]
    [InlineData("Ninguno")]
    public void UnaFuncionalidadNoDefinidaEsRechazada(string nombre)
    {
        Assert.False(ReglasFuncionalidadesCaisy.EsValida(nombre));
    }

    [Fact]
    public void LosNombresValidosSeCombinanEnUnBitmask()
    {
        var combinadas = ReglasFuncionalidadesCaisy.Combinar(["GestorPedidoAlimento"]);

        Assert.Equal(FuncionalidadesCaisy.GestorPedidoAlimento, combinadas);
    }

    [Fact]
    public void ElTokenDeGestorCaisyConFuncionalidadIncluyeElClaimDeBitmask()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), nameof(Rol.GestorCaisy), null, null,
            FuncionalidadesCaisy.GestorPedidoAlimento, out _);
        var leido = Leer(token);

        Assert.Equal(
            ((int)FuncionalidadesCaisy.GestorPedidoAlimento).ToString(),
            leido.Claims.Single(c => c.Type == ClaimsIdentidad.FuncionalidadesCaisy).Value);
    }

    [Fact]
    public void ElTokenSinFuncionalidadesNoIncluyeElClaim()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), nameof(Rol.Cliente), Guid.NewGuid(), null,
            FuncionalidadesCaisy.Ninguno, out _);
        var leido = Leer(token);

        Assert.DoesNotContain(leido.Claims, c => c.Type == ClaimsIdentidad.FuncionalidadesCaisy);
    }
}
