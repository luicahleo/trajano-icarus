namespace Trajano.GestorCaisy;

// Fecha de negocio de la aplicación de oficina: Bolivia (America/La_Paz),
// igual que el backend (spec SP8). Se usa para saber si una publicación ya
// entró en vigor y por tanto no puede anularse.
public static class FechasDeOficina
{
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}
