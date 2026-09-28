namespace Icarus.ControlAcceso.Application.Biometria;

// Resultado de extraer un vector con prueba de vida. Solo el flujo de cifrado
// del backend ve el vector; el kiosco nunca lo recibe.
public sealed record ResultadoEnrolamiento
{
    private ResultadoEnrolamiento(
        bool exitoso, byte[]? vector, string? modeloFormato, int versionModelo, string? motivo)
    {
        Exitoso = exitoso;
        Vector = vector;
        ModeloFormato = modeloFormato;
        VersionModelo = versionModelo;
        Motivo = motivo;
    }

    public bool Exitoso { get; }

    public byte[]? Vector { get; }

    public string? ModeloFormato { get; }

    public int VersionModelo { get; }

    // Código genérico (sin puntuaciones ni identidades): "pad_fallido",
    // "sin_rostro", "varios_rostros", "extraccion_fallida".
    public string? Motivo { get; }

    public static ResultadoEnrolamiento Exito(byte[] vector, string modeloFormato, int versionModelo) =>
        new(true, vector, modeloFormato, versionModelo, null);

    public static ResultadoEnrolamiento Rechazado(string motivo) =>
        new(false, null, null, 0, motivo);
}
