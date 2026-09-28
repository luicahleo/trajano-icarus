namespace Icarus.ControlAcceso.Application.Kiosco;

// Nombres de claims que escribe AutenticacionKioscoHandler (Infrastructure) y
// lee CurrentUserService del Host. Deben coincidir con ClaimsIdentidad de
// Identity; ControlAcceso no referencia ese módulo, así que se declaran aquí.
public static class ClaimsKiosco
{
    public const string Subject = "sub";
    public const string Rol = "rol";
    public const string ClienteId = "clienteId";
    public const string ExpiraUtc = "expiraUtc";
    public const string RolValor = "Kiosco";
}
