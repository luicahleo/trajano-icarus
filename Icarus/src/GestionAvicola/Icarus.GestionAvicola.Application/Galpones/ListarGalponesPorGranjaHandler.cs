using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class ListarGalponesPorGranjaHandler : IRequestHandler<ListarGalponesPorGranjaQuery, IReadOnlyList<GalponResumen>>
{
    private readonly IRepositorioGalpones _galpones; public ListarGalponesPorGranjaHandler(IRepositorioGalpones galpones) => _galpones = galpones;
    public Task<IReadOnlyList<GalponResumen>> Handle(ListarGalponesPorGranjaQuery request, CancellationToken cancellationToken) => _galpones.ListarPorGranjaAsync(request.GranjaId, cancellationToken);
}
