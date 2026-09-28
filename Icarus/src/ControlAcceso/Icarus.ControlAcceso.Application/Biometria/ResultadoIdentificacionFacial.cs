namespace Icarus.ControlAcceso.Application.Biometria;

// Decisión de identificación 1:N contra los candidatos enviados. No expone
// puntuaciones ni candidatos alternativos al llamador.
public sealed record ResultadoIdentificacionFacial
{
    private ResultadoIdentificacionFacial(bool identificado, Guid? trabajadorId, string? motivo)
    {
        Identificado = identificado;
        TrabajadorId = trabajadorId;
        Motivo = motivo;
    }

    public bool Identificado { get; }

    public Guid? TrabajadorId { get; }

    // Código genérico: "sin_coincidencia", "ambigua", "pad_fallido",
    // "modelo_incompatible", "varios_rostros".
    public string? Motivo { get; }

    public static ResultadoIdentificacionFacial Coincide(Guid trabajadorId) =>
        new(true, trabajadorId, null);

    public static ResultadoIdentificacionFacial SinCoincidencia(string motivo) =>
        new(false, null, motivo);
}
