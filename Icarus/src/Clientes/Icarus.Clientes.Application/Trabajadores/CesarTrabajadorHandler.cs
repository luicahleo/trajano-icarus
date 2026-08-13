using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class CesarTrabajadorHandler : IRequestHandler<CesarTrabajadorCommand>
{
    private readonly IRepositorioTrabajadores _trabajadores;
    private readonly IUnitOfWork _unitOfWork;

    public CesarTrabajadorHandler(IRepositorioTrabajadores trabajadores, IUnitOfWork unitOfWork)
    {
        _trabajadores = trabajadores;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CesarTrabajadorCommand request, CancellationToken cancellationToken)
    {
        var trabajador = await _trabajadores.ObtenerPorIdAsync(request.TrabajadorId, cancellationToken)
            ?? throw new NotFoundException("Trabajador", request.TrabajadorId);

        trabajador.Cesar(request.FechaCese);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
