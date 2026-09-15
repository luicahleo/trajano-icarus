using System.Security.Claims;
using Icarus.Identity.Domain;

namespace Icarus.Host.Observability;

/// <summary>Guarda el tenant y el rol permitidos en el contexto de la petición
/// para que el resumen externo los enriquezca después de que el scope interno
/// haya terminado. Solo copia claims validados; nunca claims completos.</summary>
public sealed class ContextoIdentidadObservabilidadMiddleware
{
    public const string ClienteIdItem = "Icarus.Observabilidad.ClienteId";
    public const string RolItem = "Icarus.Observabilidad.Rol";

    private readonly RequestDelegate _siguiente;

    public ContextoIdentidadObservabilidadMiddleware(RequestDelegate siguiente) =>
        _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        var clienteId = contexto.User.FindFirstValue(ClaimsIdentidad.ClienteId);
        if (Guid.TryParse(clienteId, out var cliente))
            contexto.Items[ClienteIdItem] = cliente;

        var rol = contexto.User.FindFirstValue(ClaimsIdentidad.Rol);
        if (!string.IsNullOrWhiteSpace(rol))
            contexto.Items[RolItem] = rol;

        await _siguiente(contexto);
    }
}
