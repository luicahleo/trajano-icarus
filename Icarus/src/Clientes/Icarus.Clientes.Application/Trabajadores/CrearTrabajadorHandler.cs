using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class CrearTrabajadorHandler : IRequestHandler<CrearTrabajadorCommand, Guid>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IRepositorioTrabajadores _trabajadores;
    private readonly IUnitOfWork _unitOfWork;

    public CrearTrabajadorHandler(
        IRepositorioClientes clientes, IRepositorioTrabajadores trabajadores, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _trabajadores = trabajadores;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CrearTrabajadorCommand request, CancellationToken cancellationToken)
    {
        // El filtro de tenant hace que un rol Cliente solo encuentre su propia
        // empresa: para un clienteId ajeno el resultado es el mismo 404
        // genérico que para uno inexistente (anti-enumeración).
        if (await _clientes.ObtenerPorIdAsync(request.ClienteId, cancellationToken) is null)
            throw new NotFoundException("Cliente", request.ClienteId);

        // Normaliza igual que el ctor del agregado, para que la unicidad se
        // compare contra el valor que realmente se persiste.
        var documentoIdentidad = request.DocumentoIdentidad.Trim();

        // Anti-PII: conflicto genérico, sin revelar el documento duplicado.
        if (await _trabajadores.ExisteDocumentoAsync(request.ClienteId, documentoIdentidad, cancellationToken))
            throw new ConflictException("No se pudo registrar el trabajador.");

        var trabajador = new Trabajador(
            request.ClienteId, request.Nombre, documentoIdentidad, request.Cargo, request.FechaIngreso);
        _trabajadores.Agregar(trabajador);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return trabajador.Id;
    }
}
