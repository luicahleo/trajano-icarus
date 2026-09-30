using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

// Resuelve una incidencia registrando una marcación manual para el trabajador
// indicado. Si la marcación manual falla, la incidencia permanece pendiente.
public sealed record ResolverIncidenciaAccesoCommand(
    Guid IncidenciaId,
    Guid TrabajadorId,
    TipoMarcacion Tipo,
    DateTimeOffset HoraDeclaradaUtc,
    string Motivo,
    Guid ClaveIdempotencia,
    int? VersionEsperada = null) : IRequest<Guid>;
