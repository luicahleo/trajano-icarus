using Icarus.BuildingBlocks.Application;
using Icarus.Identity.Domain;

namespace Icarus.Host.Middleware;

public sealed class ClienteActivoMiddleware
{
    private readonly RequestDelegate _siguiente;

    public ClienteActivoMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto, ICurrentUser usuario, IClienteActivo estado)
    {
        if (usuario.EstaAutenticado && ReglasRol.RequiereCliente(usuario.Rol) &&
            (usuario.ClienteId is not { } clienteId ||
             !await estado.EstaActivoAsync(clienteId, contexto.RequestAborted)))
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _siguiente(contexto);
    }
}
