namespace Icarus.Clientes.Domain;

public static class NitBoliviano
{
    public const int LongitudMaxima = 15;

    // La verificación de existencia ante el SIN se planificará al integrar verificaNit.
    public static bool TieneFormatoValido(string? nit) =>
        !string.IsNullOrWhiteSpace(nit)
        && nit.Length <= LongitudMaxima
        && nit.All(char.IsAsciiDigit);
}
