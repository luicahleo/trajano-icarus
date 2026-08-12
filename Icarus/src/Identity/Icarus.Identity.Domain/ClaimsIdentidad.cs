namespace Icarus.Identity.Domain;

// Nombres de claims del JWT. Los escribe EmisorAccessTokens (Infrastructure)
// y los lee CurrentUserService (Host): cambiar en ambos lados a la vez.
public static class ClaimsIdentidad
{
    public const string Subject = "sub";
    public const string Rol = "rol";
    public const string ClienteId = "clienteId";
}
