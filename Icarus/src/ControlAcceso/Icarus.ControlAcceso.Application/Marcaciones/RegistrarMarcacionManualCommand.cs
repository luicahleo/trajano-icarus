using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Marcaciones;

// Registro manual del cliente ante un fallo de reconocimiento. Admite fecha y
// hora pasadas o actuales (nunca futuras), con motivo y autor, y queda válido
// al guardar sin segunda aprobación ni llamada a ARGOS.
public sealed record RegistrarMarcacionManualCommand(
    Guid TrabajadorId,
    TipoMarcacion Tipo,
    DateTimeOffset HoraDeclaradaUtc,
    string Motivo,
    Guid ClaveIdempotencia) : IRequest<Guid>;
