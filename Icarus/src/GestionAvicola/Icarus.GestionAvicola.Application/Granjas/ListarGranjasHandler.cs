using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class ListarGranjasHandler : IRequestHandler<ListarGranjasQuery, IReadOnlyList<GranjaResumen>>
{
    private readonly IRepositorioGranjas _granjas;
    public ListarGranjasHandler(IRepositorioGranjas granjas) => _granjas = granjas;
    public Task<IReadOnlyList<GranjaResumen>> Handle(ListarGranjasQuery request, CancellationToken cancellationToken) => _granjas.ListarDelTenantAsync(cancellationToken);
}
