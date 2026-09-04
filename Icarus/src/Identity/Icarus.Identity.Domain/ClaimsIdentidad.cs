namespace Icarus.Identity.Domain;

// Nombres de claims del JWT. Los escribe EmisorAccessTokens (Infrastructure)
// y los lee CurrentUserService (Host): cambiar en ambos lados a la vez.
public static class ClaimsIdentidad
{
    public const string Subject = "sub";
    public const string Rol = "rol";
    public const string ClienteId = "clienteId";
    public const string TrabajadorId = "trabajadorId";

    // Bitmask de FuncionalidadesCaisy (spec SP8). Solo va en el token de las
    // cuentas de CAISY con alguna función asignada.
    public const string FuncionalidadesCaisy = "funcCaisy";
}
