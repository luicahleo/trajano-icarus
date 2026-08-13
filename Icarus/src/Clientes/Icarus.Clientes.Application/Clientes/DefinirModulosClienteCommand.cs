using MediatR;

namespace Icarus.Clientes.Application.Clientes;

// Asignación de módulos (entitlement), exclusiva del Administrador (spec).
// La lista reemplaza al conjunto actual; una lista vacía quita todos los
// módulos.
public sealed record DefinirModulosClienteCommand(Guid ClienteId, IReadOnlyList<string> Modulos) : IRequest;
