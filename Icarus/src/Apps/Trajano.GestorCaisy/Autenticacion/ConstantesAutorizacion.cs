namespace Trajano.GestorCaisy.Autenticacion;

// Nombres que la aplicación de oficina usa del contrato con Trajano-Icarus:
// claims emitidos en el access token (rol y funcionalidades CAISY) y los
// tokens que viajan protegidos dentro de la cookie de sesión.
public static class ConstantesAutorizacion
{
    public const string RolGestorCaisy = "GestorCaisy";

    // Bitmask FuncionalidadesCaisy del backend: GestorPedidoAlimento = 1.
    public const int BitGestorPedidoAlimento = 1;

    public const string PoliticaGestorPedidoAlimento = "GestorPedidoAlimento";

    public const string ClaimSub = "sub";
    public const string ClaimRol = "rol";
    public const string ClaimFuncionalidadesCaisy = "funcCaisy";
    public const string ClaimCorreo = "correo";
    public const string ClaimAccessToken = "accessToken";
    public const string ClaimRefreshToken = "refreshToken";
}
