using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed record ListarIncidenciasAccesoQuery(
    EstadoIncidenciaAcceso? Estado = null,
    int Pagina = 1,
    int TamanoPagina = 25) : IRequest<Pagina<IncidenciaResumen>>;
