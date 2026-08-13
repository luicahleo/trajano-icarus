using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed class SuspenderClienteHandler : IRequestHandler<SuspenderClienteCommand>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnitOfWork _unitOfWork;

    public SuspenderClienteHandler(IRepositorioClientes clientes, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SuspenderClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clientes.ObtenerGestionablePorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new NotFoundException("Cliente", request.ClienteId);

        cliente.Suspender();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
