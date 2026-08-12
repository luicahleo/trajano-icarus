namespace Icarus.Identity.Infrastructure.Autenticacion;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    // Clave simétrica HMAC-SHA256 (mínimo 32 caracteres). La de desarrollo vive
    // en appsettings.Development.json; fuera de dev se inyecta por configuración
    // del entorno. Nunca se registra en logs.
    public string Clave { get; set; } = string.Empty;
    public string Emisor { get; set; } = "Icarus";
    public string Audiencia { get; set; } = "Icarus";
    public int MinutosAccessToken { get; set; } = 15;
    public int DiasRefreshToken { get; set; } = 7;
}
