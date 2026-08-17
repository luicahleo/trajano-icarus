using Icarus.BuildingBlocks.Application;
using Icarus.Identity.Domain;
using MediatR;

namespace Icarus.Identity.Application.Sesiones;

public sealed class RenovarSesionHandler : IRequestHandler<RenovarSesionCommand, ResultadoSesion>
{
    private readonly IServicioRefreshTokens _refresh;
    private readonly IConsultaUsuarios _consulta;
    private readonly IEmisorAccessTokens _emisor;
    private readonly IClienteActivo _estadoCliente;

    public RenovarSesionHandler(
        IServicioRefreshTokens refresh, IConsultaUsuarios consulta, IEmisorAccessTokens emisor,
        IClienteActivo estadoCliente)
    {
        _refresh = refresh;
        _consulta = consulta;
        _emisor = emisor;
        _estadoCliente = estadoCliente;
    }

    public async Task<ResultadoSesion> Handle(RenovarSesionCommand request, CancellationToken cancellationToken)
    {
        var usuarioId = await _refresh.RotarAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var usuario = await _consulta.ObtenerPorIdAsync(usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        if (ReglasRol.RequiereCliente(usuario.Rol) && usuario.ClienteId is null)
            throw new UnauthorizedAccessException("La sesión no es válida.");

        if (usuario.ClienteId is { } clienteId &&
            !await _estadoCliente.EstaActivoAsync(clienteId, cancellationToken))
            throw new UnauthorizedAccessException("La sesión no es válida.");

        var accessToken = _emisor.Emitir(
            usuario.Id, usuario.Rol, usuario.ClienteId, usuario.TrabajadorId, out var expiraEnSegundos);
        var nuevoRefresh = await _refresh.EmitirAsync(usuario.Id, cancellationToken);
        return new ResultadoSesion(accessToken, nuevoRefresh, expiraEnSegundos);
    }
}
