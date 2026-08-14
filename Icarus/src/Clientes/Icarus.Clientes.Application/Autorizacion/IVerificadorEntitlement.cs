using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Autorizacion;

// Entitlement (spec): trabajadorId null => semántica de rol Cliente (todas las
// funcionalidades de los módulos de su cliente); trabajadorId presente => solo
// sus funcionalidades asignadas (rol Trabajador). Clientes no conoce los
// nombres de rol de Identity: es la presencia del claim la que decide.
public interface IVerificadorEntitlement
{
    Task<bool> TieneFuncionalidadAsync(
        Guid clienteId, Guid? trabajadorId, Funcionalidades funcionalidad,
        CancellationToken cancellationToken = default);
}
