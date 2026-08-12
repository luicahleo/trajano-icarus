using Icarus.Identity.Application.Sesiones;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Identity;

public class RenovarSesionHandlerTests
{
    private readonly IServicioRefreshTokens _refresh = Substitute.For<IServicioRefreshTokens>();
    private readonly IConsultaUsuarios _consulta = Substitute.For<IConsultaUsuarios>();
    private readonly IEmisorAccessTokens _emisor = Substitute.For<IEmisorAccessTokens>();
    private readonly RenovarSesionHandler _handler;

    public RenovarSesionHandlerTests()
    {
        _handler = new RenovarSesionHandler(_refresh, _consulta, _emisor);
        _emisor.Emitir(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), out Arg.Any<int>())
            .Returns(call =>
            {
                call[3] = 900;
                return "access-token-nuevo";
            });
    }

    [Fact]
    public async Task RefreshValidoRotaYDevuelveNuevaSesion()
    {
        var usuarioId = Guid.NewGuid();
        _refresh.RotarAsync("refresh-viejo", Arg.Any<CancellationToken>()).Returns(usuarioId);
        _consulta.ObtenerPorIdAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(new UsuarioResumen(usuarioId, "cuenta@icarus.test", "Cliente", Guid.NewGuid()));
        _refresh.EmitirAsync(usuarioId, Arg.Any<CancellationToken>()).Returns("refresh-nuevo");

        var sesion = await _handler.Handle(new RenovarSesionCommand("refresh-viejo"), CancellationToken.None);

        Assert.Equal("access-token-nuevo", sesion.AccessToken);
        Assert.Equal("refresh-nuevo", sesion.RefreshToken);
    }

    [Fact]
    public async Task RefreshInvalidoLanzaUnauthorized()
    {
        _refresh.RotarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new RenovarSesionCommand("refresh-malo"), CancellationToken.None));
    }

    [Fact]
    public async Task UsuarioInactivoONoEncontradoLanzaUnauthorized()
    {
        _refresh.RotarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        _consulta.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UsuarioResumen?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new RenovarSesionCommand("refresh-viejo"), CancellationToken.None));
    }
}
