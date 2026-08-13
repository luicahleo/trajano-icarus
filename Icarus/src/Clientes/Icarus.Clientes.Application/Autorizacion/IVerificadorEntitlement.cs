using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Autorizacion;

// Entitlement (spec): un endpoint de un módulo de negocio exige que el cliente
// del usuario tenga ese módulo habilitado y esté activo.
public interface IVerificadorEntitlement
{
    Task<bool> TieneModuloHabilitadoAsync(
        Guid clienteId, Modulos modulo, CancellationToken cancellationToken = default);
}
