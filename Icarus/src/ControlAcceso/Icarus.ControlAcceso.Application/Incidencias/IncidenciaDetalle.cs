using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Incidencias;

// Detalle de una incidencia para su revisión y resolución. No expone
// identidad supuesta ni evidencia facial.
public sealed record IncidenciaDetalle(
    Guid Id,
    Guid FlujoMarcacionId,
    Guid SesionKioscoId,
    TipoMarcacion Accion,
    DateTimeOffset PrimerRechazoUtc,
    DateTimeOffset TercerRechazoUtc,
    EstadoIncidenciaAcceso Estado,
    Guid? TrabajadorId,
    Guid? JornadaId,
    string? MotivoResolucion,
    DateTimeOffset? ResueltaEnUtc,
    int Version);
