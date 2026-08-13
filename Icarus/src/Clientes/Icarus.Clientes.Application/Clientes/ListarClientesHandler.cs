using MediatR;

namespace Icarus.Clientes.Application.Clientes;

public sealed class ListarClientesHandler : IRequestHandler<ListarClientesQuery, IReadOnlyList<ClienteResumen>>
{
    private readonly IRepositorioClientes _clientes;

    public ListarClientesHandler(IRepositorioClientes clientes) => _clientes = clientes;

    public Task<IReadOnlyList<ClienteResumen>> Handle(
        ListarClientesQuery request, CancellationToken cancellationToken) =>
        _clientes.ListarTodosAsync(cancellationToken);
}
