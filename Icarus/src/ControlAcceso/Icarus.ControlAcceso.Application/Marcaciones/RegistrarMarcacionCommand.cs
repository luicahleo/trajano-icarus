using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Marcaciones;

// Marcación del kiosco. El trabajador se identifica con la muestra; nunca se
// envía un TrabajadorId desde el dispositivo.
public sealed record RegistrarMarcacionCommand(
    TipoMarcacion Accion,
    byte[] Muestra,
    string Formato,
    Guid ClaveIdempotencia) : IRequest<ResultadoMarcacion>;

// Segunda confirmación de la Salida propuesta con la hora actual.
public sealed record ConfirmarSalidaCommand(Guid PropuestaId) : IRequest<ResultadoMarcacion>;

public enum EstadoResultadoMarcacion
{
    Registrada,
    PropuestaSalida,
    Rechazada
}

// Respuesta del kiosco: nombre efímero y datos del evento. Nunca incluye
// puntuaciones, vectores ni candidatos alternativos.
public sealed record ResultadoMarcacion(
    EstadoResultadoMarcacion Estado,
    Guid? TrabajadorId,
    string? NombreCompleto,
    TipoMarcacion? Tipo,
    DateTimeOffset? InstanteUtc,
    Guid? PropuestaId,
    DateTimeOffset? ExpiraPropuestaUtc,
    string? Motivo);
