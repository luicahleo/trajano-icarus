using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed record ObtenerIncidenciaAccesoQuery(Guid Id) : IRequest<IncidenciaDetalle>;
