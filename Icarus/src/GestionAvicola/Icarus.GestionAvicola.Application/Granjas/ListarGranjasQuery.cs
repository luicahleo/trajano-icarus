using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record ListarGranjasQuery : IRequest<IReadOnlyList<GranjaResumen>>;
