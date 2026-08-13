using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed class DefinirModulosClienteHandler : IRequestHandler<DefinirModulosClienteCommand>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnitOfWork _unitOfWork;

    public DefinirModulosClienteHandler(IRepositorioClientes clientes, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DefinirModulosClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await _clientes.ObtenerGestionablePorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new NotFoundException("Cliente", request.ClienteId);

        // El validador garantiza que cada nombre es un valor del enum.
        var modulos = request.Modulos.Aggregate(
            Modulos.Ninguno, (acumulado, nombre) => acumulado | Enum.Parse<Modulos>(nombre, ignoreCase: true));
        cliente.DefinirModulos(modulos);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
