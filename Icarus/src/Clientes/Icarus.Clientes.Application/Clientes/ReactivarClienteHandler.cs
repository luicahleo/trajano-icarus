using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed class ReactivarClienteHandler : IRequestHandler<ReactivarClienteCommand>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivarClienteHandler(IRepositorioClientes clientes, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ReactivarClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clientes.ObtenerGestionablePorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new NotFoundException("Cliente", request.ClienteId);

        cliente.Reactivar();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
