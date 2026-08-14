using MediatR;

namespace Icarus.Identity.Application.Sesiones;

public sealed class IniciarSesionHandler : IRequestHandler<IniciarSesionCommand, ResultadoSesion>
{
    private readonly IVerificadorCredenciales _verificador;
    private readonly IEmisorAccessTokens _emisor;
    private readonly IServicioRefreshTokens _refresh;

    public IniciarSesionHandler(
        IVerificadorCredenciales verificador,
        IEmisorAccessTokens emisor,
        IServicioRefreshTokens refresh)
    {
        _verificador = verificador;
        _emisor = emisor;
        _refresh = refresh;
    }

    public async Task<ResultadoSesion> Handle(IniciarSesionCommand request, CancellationToken cancellationToken)
    {
        // Anti-enumeración y anti-PII: un solo mensaje genérico para cualquier fallo.
        var credencial = await _verificador.VerificarAsync(request.Email, request.Contrasena, cancellationToken)
            ?? throw new UnauthorizedAccessException("Credenciales inválidas.");

        var accessToken = _emisor.Emitir(
            credencial.UsuarioId, credencial.Rol, credencial.ClienteId, credencial.TrabajadorId,
            out var expiraEnSegundos);
        var refreshToken = await _refresh.EmitirAsync(credencial.UsuarioId, cancellationToken);
        return new ResultadoSesion(accessToken, refreshToken, expiraEnSegundos);
    }
}
