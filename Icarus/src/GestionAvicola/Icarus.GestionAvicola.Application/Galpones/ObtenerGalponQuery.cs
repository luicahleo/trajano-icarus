using MediatR;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed record ObtenerGalponQuery(Guid GalponId) : IRequest<GalponResumen>;
