using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class DesactivarTrabajadorHandler : IRequestHandler<DesactivarTrabajadorCommand>
{
    private readonly IRepositorioTrabajadores _trabajadores;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarTrabajadorHandler(IRepositorioTrabajadores trabajadores, IUnitOfWork unitOfWork)
    {
        _trabajadores = trabajadores;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DesactivarTrabajadorCommand request, CancellationToken cancellationToken)
    {
        var trabajador = await _trabajadores.ObtenerPorIdAsync(request.TrabajadorId, cancellationToken)
            ?? throw new NotFoundException("Trabajador", request.TrabajadorId);

        trabajador.Desactivar();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
