using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed class CrearClienteHandler : IRequestHandler<CrearClienteCommand, Guid>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnitOfWork _unitOfWork;

    public CrearClienteHandler(IRepositorioClientes clientes, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CrearClienteCommand request, CancellationToken cancellationToken)
    {
        // Normaliza igual que el ctor del agregado, para que la unicidad se
        // compare contra el valor que realmente se persiste.
        var identificadorFiscal = request.IdentificadorFiscal.Trim();

        // Anti-PII: conflicto genérico, sin revelar el dato duplicado.
        if (await _clientes.ExisteIdentificadorFiscalAsync(identificadorFiscal, cancellationToken))
            throw new ConflictException("No se pudo registrar el cliente.");

        var cliente = new Cliente(request.RazonSocial, identificadorFiscal);
        _clientes.Agregar(cliente);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return cliente.Id;
    }
}
