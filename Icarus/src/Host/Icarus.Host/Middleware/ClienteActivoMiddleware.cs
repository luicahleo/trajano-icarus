using Icarus.BuildingBlocks.Application;

namespace Icarus.Host.Middleware;

public sealed class ClienteActivoMiddleware
{
    private readonly RequestDelegate _siguiente;

    public ClienteActivoMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto, ICurrentUser usuario, IClienteActivo estado)
    {
        if (usuario.EstaAutenticado && usuario.ClienteId is { } clienteId &&
            !await estado.EstaActivoAsync(clienteId, contexto.RequestAborted))
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _siguiente(contexto);
    }
}
