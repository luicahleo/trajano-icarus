using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Domain;
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class DefinirFuncionalidadesTrabajadorHandler : IRequestHandler<DefinirFuncionalidadesTrabajadorCommand>
{
    private readonly IRepositorioClientes _clientes;
    private readonly IRepositorioTrabajadores _trabajadores;
    private readonly IUnitOfWork _unitOfWork;

    public DefinirFuncionalidadesTrabajadorHandler(
        IRepositorioClientes clientes, IRepositorioTrabajadores trabajadores, IUnitOfWork unitOfWork)
    {
        _clientes = clientes;
        _trabajadores = trabajadores;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DefinirFuncionalidadesTrabajadorCommand request, CancellationToken cancellationToken)
    {
        // El filtro de tenant hace que un rol Cliente solo alcance su propia
        // empresa y trabajadores: ids ajenos dan el mismo 404 (anti-enumeración).
        var cliente = await _clientes.ObtenerPorIdAsync(request.ClienteId, cancellationToken)
            ?? throw new NotFoundException("Cliente", request.ClienteId);
        var trabajador = await _trabajadores.ObtenerPorIdAsync(request.TrabajadorId, cancellationToken)
            ?? throw new NotFoundException("Trabajador", request.TrabajadorId);

        // La regla «solo funcionalidades de módulos habilitados para el cliente»
        // es transversal (necesita el cliente) y se valida aquí, no en el
        // agregado (spec). Mensaje genérico: no revela la funcionalidad.
        var asignadas = Funcionalidades.Ninguno;
        foreach (var nombre in request.Funcionalidades)
        {
            var funcionalidad = Enum.Parse<Funcionalidades>(nombre, ignoreCase: true);
            if (!FuncionalidadesTrabajador.EsAsignable(funcionalidad))
                throw new ReglaNegocioException("Funcionalidad no disponible para este cliente.");
            if (!cliente.TieneModulo(FuncionalidadesModulos.ModuloDe(funcionalidad)))
                throw new ReglaNegocioException("Funcionalidad no disponible para este cliente.");
            asignadas |= funcionalidad;
        }

        trabajador.DefinirFuncionalidades(asignadas);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
