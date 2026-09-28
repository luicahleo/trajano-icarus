using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

// Extrae un vector con prueba de vida, lo cifra y habilita al trabajador. La
// muestra nunca se persiste ni se registra.
public sealed record EnrolarTrabajadorCommand(
    Guid TrabajadorId,
    byte[] Muestra,
    string Formato,
    Guid ClaveIdempotencia) : IRequest<ResultadoEnrolarTrabajador>;

public sealed record ResultadoEnrolarTrabajador(
    bool Exitoso, int VersionEnrolamiento, string? Motivo);
