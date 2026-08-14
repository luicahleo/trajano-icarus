using Icarus.Identity.Application.Sesiones;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Identity;

public class IniciarSesionHandlerTests
{
    private readonly IVerificadorCredenciales _verificador = Substitute.For<IVerificadorCredenciales>();
    private readonly IEmisorAccessTokens _emisor = Substitute.For<IEmisorAccessTokens>();
    private readonly IServicioRefreshTokens _refresh = Substitute.For<IServicioRefreshTokens>();
    private readonly IniciarSesionHandler _handler;

    public IniciarSesionHandlerTests()
    {
        _handler = new IniciarSesionHandler(_verificador, _emisor, _refresh);
        _emisor.Emitir(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), out Arg.Any<int>())
            .Returns(call =>
            {
                call[4] = 900;
                return "access-token";
            });
        _refresh.EmitirAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
    }

    [Fact]
    public async Task CredencialesValidasDevuelvenSesionCompleta()
    {
        var usuarioId = Guid.NewGuid();
        _verificador.VerificarAsync("cuenta@icarus.test", "x", Arg.Any<CancellationToken>())
            .Returns(new CredencialValida(usuarioId, "Cliente", Guid.NewGuid(), null));

        var sesion = await _handler.Handle(
            new IniciarSesionCommand("cuenta@icarus.test", "x"), CancellationToken.None);

        Assert.Equal("access-token", sesion.AccessToken);
        Assert.Equal("refresh-token", sesion.RefreshToken);
        Assert.Equal(900, sesion.ExpiraEnSegundos);
    }

    [Fact]
    public async Task CredencialesInvalidasLanzanUnauthorizedGenerico()
    {
        _verificador.VerificarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CredencialValida?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new IniciarSesionCommand("cuenta@icarus.test", "x"), CancellationToken.None));

        Assert.Equal("Credenciales inválidas.", ex.Message);
    }
}
