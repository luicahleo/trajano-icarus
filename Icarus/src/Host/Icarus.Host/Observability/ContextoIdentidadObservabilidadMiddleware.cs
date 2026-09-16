using System.Security.Claims;
using Icarus.BuildingBlocks.Observability;
using Icarus.Identity.Domain;
using Serilog.Context;

namespace Icarus.Host.Observability;

/// <summary>Publica el tenant y el rol permitidos como contexto de todos los
/// eventos de la ejecución (operación, decisión, persistencia y transacción),
/// además de guardarlos en Items para que el resumen externo y el log de error
/// exterior los enriquezcan después de que el scope interno haya terminado.
/// Solo copia claims validados; nunca claims completos ni datos nominales.</summary>
public sealed class ContextoIdentidadObservabilidadMiddleware
{
    private readonly RequestDelegate _siguiente;

    public ContextoIdentidadObservabilidadMiddleware(RequestDelegate siguiente) =>
        _siguiente = siguiente;

    public async Task Invoke(HttpContext contexto)
    {
        Guid? clienteId = null;
        if (Guid.TryParse(contexto.User.FindFirstValue(ClaimsIdentidad.ClienteId), out var cliente))
        {
            clienteId = cliente;
            DiagnosticContext.EstablecerClienteId(contexto, cliente);
        }

        var rol = contexto.User.FindFirstValue(ClaimsIdentidad.Rol);
        if (string.IsNullOrWhiteSpace(rol))
            rol = null;
        else
            DiagnosticContext.EstablecerRol(contexto, rol);

        // El scope se dispone siempre, también ante excepción.
        var ambitos = new List<IDisposable>(2);
        try
        {
            if (clienteId is not null)
                ambitos.Add(LogContext.PushProperty("ClienteId", clienteId.Value));
            if (rol is not null)
                ambitos.Add(LogContext.PushProperty("Rol", rol));
            await _siguiente(contexto);
        }
        finally
        {
            for (var i = ambitos.Count - 1; i >= 0; i--)
                ambitos[i].Dispose();
        }
    }
}
