using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record ObtenerGranjaQuery(Guid GranjaId) : IRequest<GranjaResumen>;
