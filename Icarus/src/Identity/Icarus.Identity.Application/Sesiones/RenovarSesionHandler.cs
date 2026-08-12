using MediatR;

namespace Icarus.Identity.Application.Sesiones;

public sealed class RenovarSesionHandler : IRequestHandler<RenovarSesionCommand, ResultadoSesion>
{
    private readonly IServicioRefreshTokens _refresh;
    private readonly IConsultaUsuarios _consulta;
    private readonly IEmisorAccessTokens _emisor;

    public RenovarSesionHandler(
        IServicioRefreshTokens refresh, IConsultaUsuarios consulta, IEmisorAccessTokens emisor)
    {
        _refresh = refresh;
        _consulta = consulta;
        _emisor = emisor;
    }

    public async Task<ResultadoSesion> Handle(RenovarSesionCommand request, CancellationToken cancellationToken)
    {
        var usuarioId = await _refresh.RotarAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var usuario = await _consulta.ObtenerPorIdAsync(usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var accessToken = _emisor.Emitir(usuario.Id, usuario.Rol, usuario.ClienteId, out var expiraEnSegundos);
        var nuevoRefresh = await _refresh.EmitirAsync(usuario.Id, cancellationToken);
        return new ResultadoSesion(accessToken, nuevoRefresh, expiraEnSegundos);
    }
}
