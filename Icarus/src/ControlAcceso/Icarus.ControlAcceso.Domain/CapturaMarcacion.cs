using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Resultado durable de una captura dentro de un flujo de marcación. No guarda
// la muestra, solo la decisión genérica y la clave que la identifica.
public sealed class CapturaMarcacion : Entity
{
    private CapturaMarcacion()
    {
    }

    private CapturaMarcacion(
        Guid claveCaptura,
        ResultadoCapturaMarcacion resultado,
        Guid? trabajadorId,
        string? motivo,
        DateTimeOffset instanteUtc)
    {
        if (claveCaptura == Guid.Empty)
            throw new ReglaNegocioException("La captura requiere una clave.");
        if (resultado == ResultadoCapturaMarcacion.Rechazada &&
            string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("Un rechazo requiere un motivo genérico.");

        ClaveCaptura = claveCaptura;
        Resultado = resultado;
        TrabajadorId = trabajadorId;
        Motivo = motivo;
        InstanteUtc = instanteUtc;
    }

    public Guid ClaveCaptura { get; private set; }

    public ResultadoCapturaMarcacion Resultado { get; private set; }

    public Guid? TrabajadorId { get; private set; }

    // Motivo genérico de rechazo (sin muestras ni datos nominales).
    public string? Motivo { get; private set; }

    public DateTimeOffset InstanteUtc { get; private set; }

    public bool EsRechazoFacial => Resultado == ResultadoCapturaMarcacion.Rechazada;

    public static CapturaMarcacion Identificada(
        Guid claveCaptura, Guid trabajadorId, DateTimeOffset instanteUtc) =>
        new(claveCaptura, ResultadoCapturaMarcacion.Identificada, trabajadorId, null, instanteUtc);

    public static CapturaMarcacion Rechazada(
        Guid claveCaptura, string motivo, DateTimeOffset instanteUtc) =>
        new(claveCaptura, ResultadoCapturaMarcacion.Rechazada, null, motivo, instanteUtc);
}
