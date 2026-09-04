namespace Trajano.GestorCaisy.Servicios;

// Error de la API de Trajano-Icarus. Transporta solo referencias técnicas
// (estado, título genérico del ProblemDetails, id de correlación y errores de
// validación del negocio): nunca cuerpos de documentos, credenciales ni
// tokens (anti-PII).
public sealed class ErrorApiException : Exception
{
    public ErrorApiException()
    {
    }

    public ErrorApiException(string mensaje)
        : base(mensaje)
    {
    }

    public ErrorApiException(string mensaje, Exception interna)
        : base(mensaje, interna)
    {
    }

    public ErrorApiException(
        int estado, string? titulo = null, string? idCorrelacion = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? erroresValidacion = null)
        : base(titulo ?? $"La API respondió con el estado {estado}.")
    {
        Estado = estado;
        Titulo = titulo;
        IdCorrelacion = idCorrelacion;
        ErroresValidacion = erroresValidacion;
    }

    public int Estado { get; }

    public string? Titulo { get; }

    public string? IdCorrelacion { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ErroresValidacion { get; }

    public string MensajeParaLaInterfaz() =>
        ErroresValidacion is { } errores && errores.Count > 0
            ? string.Join(" ", errores.Values.SelectMany(v => v))
            : Titulo ?? $"La API respondió con el estado {Estado}.";
}
