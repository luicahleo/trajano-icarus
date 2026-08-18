using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed record ListarGalponesPorGranjaQuery(Guid GranjaId) : IRequest<IReadOnlyList<GalponResumen>>;
