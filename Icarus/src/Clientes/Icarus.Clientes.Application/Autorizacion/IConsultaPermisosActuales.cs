namespace Icarus.Clientes.Application.Autorizacion;

// Lectura de entitlement para /identidad/me: la PWA necesita saber qué
// mostrar sin sondear 403. La implementación vive en Infrastructure junto a
// VerificadorEntitlement (misma fuente de datos).
public interface IConsultaPermisosActuales
{
    Task<PermisosActuales> ObtenerAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default);
}
