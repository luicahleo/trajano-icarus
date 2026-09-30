using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Incidencias;

// Resumen de incidencia para la bandeja del cliente. Sin nombre ni datos
// biométricos; la UI construye el mensaje visible a partir de estos metadatos.
public sealed record IncidenciaResumen(
    Guid Id,
    Guid FlujoMarcacionId,
    TipoMarcacion Accion,
    DateTimeOffset PrimerRechazoUtc,
    DateTimeOffset TercerRechazoUtc,
    EstadoIncidenciaAcceso Estado,
    int Version);
