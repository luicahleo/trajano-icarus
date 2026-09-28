using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed record ListarJornadasQuery(
    Guid? TrabajadorId,
    DateOnly? Desde,
    DateOnly? Hasta,
    int Pagina = 1,
    int TamanoPagina = 20) : IRequest<Pagina<JornadaResumen>>;

// Resumen para el historial: sin nombres ni documentos (anti-PII); el Host
// resuelve el nombre del trabajador con una consulta autorizada.
public sealed record JornadaResumen(
    Guid Id,
    Guid TrabajadorId,
    DateOnly FechaBoliviana,
    EstadoJornadaAcceso Estado,
    int CantidadMarcaciones,
    int Version);
