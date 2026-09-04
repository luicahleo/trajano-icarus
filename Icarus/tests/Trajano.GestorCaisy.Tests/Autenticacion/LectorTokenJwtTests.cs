using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Autenticacion;

public class LectorTokenJwtTests
{
    [Fact]
    public void LeeSujetoRolFuncionalidadesYVencimiento()
    {
        var vencimiento = DateTimeOffset.UtcNow.AddMinutes(10);
        var token = CreadorTokens.Crear(rol: "GestorCaisy", funcCaisy: 1,
            sujeto: "sujeto-de-prueba", expira: vencimiento);

        var encabezado = LectorTokenJwt.Leer(token);

        Assert.NotNull(encabezado);
        Assert.Equal("sujeto-de-prueba", encabezado.SujetoId);
        Assert.Equal("GestorCaisy", encabezado.Rol);
        Assert.Equal(1, encabezado.FuncionalidadesCaisy);
        Assert.NotNull(encabezado.ExpiraEn);
    }

    [Fact]
    public void SinFuncCaisyDevuelveFuncionalidadesNulas()
    {
        var token = CreadorTokens.Crear(rol: "Cliente", funcCaisy: null);

        var encabezado = LectorTokenJwt.Leer(token);

        Assert.NotNull(encabezado);
        Assert.Equal("Cliente", encabezado.Rol);
        Assert.Null(encabezado.FuncionalidadesCaisy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-token")]
    [InlineData("a.b")]
    [InlineData("aGVsbG8.d29ybGQ.firma")]
    public void TokenMalformadoDevuelveNulo(string token)
    {
        Assert.Null(LectorTokenJwt.Leer(token));
    }
}
