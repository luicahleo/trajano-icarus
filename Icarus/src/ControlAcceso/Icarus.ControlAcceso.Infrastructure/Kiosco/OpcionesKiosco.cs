namespace Icarus.ControlAcceso.Infrastructure.Kiosco;

public sealed class OpcionesKiosco
{
    public const string Seccion = "ControlAcceso:Kiosco";
    public const string Esquema = "Kiosco";
    public const string Cookie = "icarus_kiosco";
    public const string EncabezadoAntiforgery = "X-Icarus-Kiosco";

    // Orígenes web autorizados a operar el kiosco. Un origen distinto se
    // rechaza aunque la cookie viaje (defensa CSRF).
    public string[] OrigenesPermitidos { get; set; } = [];

    public int HorasVigencia { get; set; } = 12;

    // Tope de intentos de activación por IP y minuto (anti fuerza bruta).
    public int MaximoActivacionesPorMinuto { get; set; } = 10;
}
