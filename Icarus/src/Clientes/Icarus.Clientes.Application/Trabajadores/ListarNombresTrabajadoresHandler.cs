using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class ListarNombresTrabajadoresHandler
    : IRequestHandler<ListarNombresTrabajadoresQuery, IReadOnlyList<TrabajadorNombreResumen>>
{
    private readonly IRepositorioTrabajadores _trabajadores;

    public ListarNombresTrabajadoresHandler(IRepositorioTrabajadores trabajadores) =>
        _trabajadores = trabajadores;

    public async Task<IReadOnlyList<TrabajadorNombreResumen>> Handle(
        ListarNombresTrabajadoresQuery request, CancellationToken cancellationToken)
    {
        var trabajadores = await _trabajadores.ListarPorClienteAsync(request.ClienteId, cancellationToken);
        return trabajadores.Select(t => new TrabajadorNombreResumen(t.Id, t.Nombre)).ToList();
    }
}
