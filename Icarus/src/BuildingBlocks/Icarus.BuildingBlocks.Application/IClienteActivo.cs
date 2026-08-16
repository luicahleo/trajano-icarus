namespace Icarus.BuildingBlocks.Application;

public interface IClienteActivo
{
    Task<bool> EstaActivoAsync(Guid clienteId, CancellationToken cancellationToken = default);
}
