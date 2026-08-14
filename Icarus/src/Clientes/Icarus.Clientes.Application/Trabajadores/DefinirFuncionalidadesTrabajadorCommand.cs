using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

// Asignación de funcionalidades operativas al trabajador (entitlement),
// exclusiva del Cliente sobre su propia empresa (spec). La lista reemplaza al
// conjunto actual.
public sealed record DefinirFuncionalidadesTrabajadorCommand(
    Guid ClienteId, Guid TrabajadorId, IReadOnlyList<string> Funcionalidades) : IRequest;
