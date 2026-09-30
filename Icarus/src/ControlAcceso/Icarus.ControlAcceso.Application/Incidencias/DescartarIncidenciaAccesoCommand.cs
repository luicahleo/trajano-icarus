using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

// Descarta una incidencia pendiente con motivo. No crea marcación.
public sealed record DescartarIncidenciaAccesoCommand(
    Guid IncidenciaId,
    string Motivo,
    Guid ClaveIdempotencia,
    int? VersionEsperada = null) : IRequest;
