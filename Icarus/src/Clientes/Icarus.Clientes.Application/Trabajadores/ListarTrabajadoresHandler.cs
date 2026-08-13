using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class ListarTrabajadoresHandler
    : IRequestHandler<ListarTrabajadoresQuery, IReadOnlyList<TrabajadorResumen>>
{
    private readonly IRepositorioTrabajadores _trabajadores;

    public ListarTrabajadoresHandler(IRepositorioTrabajadores trabajadores) => _trabajadores = trabajadores;

    public Task<IReadOnlyList<TrabajadorResumen>> Handle(
        ListarTrabajadoresQuery request, CancellationToken cancellationToken) =>
        _trabajadores.ListarPorClienteAsync(request.ClienteId, cancellationToken);
}
