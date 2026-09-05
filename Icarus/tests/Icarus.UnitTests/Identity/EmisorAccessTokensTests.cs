using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Icarus.UnitTests.Identity;

public class EmisorAccessTokensTests
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
    public void TokenIncluyeSubRolYClienteId()
    {
        var usuarioId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        var token = CrearEmisor().Emitir(
            usuarioId, "Cliente", clienteId, null, FuncionalidadesCaisy.Ninguno, out var expiraEnSegundos);
        var leido = Leer(token);

        Assert.Equal(usuarioId.ToString(), leido.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("Cliente", leido.Claims.Single(c => c.Type == "rol").Value);
        Assert.Equal(clienteId.ToString(), leido.Claims.Single(c => c.Type == "clienteId").Value);
        Assert.Equal(900, expiraEnSegundos);
    }

    [Fact]
    public void TokenSinClienteOmiteElClaimClienteId()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), "Administrador", null, null, FuncionalidadesCaisy.Ninguno, out _);
        var leido = Leer(token);

        Assert.DoesNotContain(leido.Claims, c => c.Type == "clienteId");
    }

    [Fact]
    public void TokenConTrabajadorIncluyeElClaimTrabajadorId()
    {
        var trabajadorId = Guid.NewGuid();

        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), "Trabajador", Guid.NewGuid(), trabajadorId,
            FuncionalidadesCaisy.Ninguno, out _);
        var leido = Leer(token);

        Assert.Equal(trabajadorId.ToString(), leido.Claims.Single(c => c.Type == "trabajadorId").Value);
    }

    [Fact]
    public void TokenSinTrabajadorOmiteElClaimTrabajadorId()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), "Cliente", Guid.NewGuid(), null, FuncionalidadesCaisy.Ninguno, out _);
        var leido = Leer(token);

        Assert.DoesNotContain(leido.Claims, c => c.Type == "trabajadorId");
    }

    [Fact]
    public void TokenLlevaEmisorYAudienciaConfigurados()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), "Administrador", null, null, FuncionalidadesCaisy.Ninguno, out _);
        var leido = Leer(token);

        Assert.Equal("Icarus", leido.Issuer);
        Assert.Contains("Icarus", leido.Audiences);
    }
}
