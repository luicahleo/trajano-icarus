using System.Security.Claims;

namespace Trajano.GestorCaisy.Autenticacion;

// Comprobación compartida de rol y funcionalidad: la usa la política de
// autorización, el menú de la barra lateral y el aterrizaje tras iniciar
// sesión (un menú limitado por función no sustituye la autorización del
// servidor, solo la complementa).
public static class ReclamosCaisy
{
    public static bool TieneGestorPedidoAlimento(ClaimsPrincipal usuario) =>
        TieneFuncionalidad(usuario, ConstantesAutorizacion.BitGestorPedidoAlimento);

    public static bool TieneGestorRecepcionHuevos(ClaimsPrincipal usuario) =>
        TieneFuncionalidad(usuario, ConstantesAutorizacion.BitGestorRecepcionHuevos);

    public static bool TieneGestorVeterinario(ClaimsPrincipal usuario) =>
        TieneFuncionalidad(usuario, ConstantesAutorizacion.BitGestorVeterinario);

    public static bool TieneAlgunaFuncionalidad(ClaimsPrincipal usuario) =>
        TieneGestorPedidoAlimento(usuario)
        || TieneGestorRecepcionHuevos(usuario)
        || TieneGestorVeterinario(usuario);

    // Primera sección que la cuenta puede usar (spec 2026-10-30): sin esto
    // una cuenta solo veterinaria aterrizaría en «Acceso restringido». Sin
    // ninguna funcionalidad se conserva Precios y la política la manda a
    // /Sesion/Denegado.
    public static string ControladorInicial(ClaimsPrincipal usuario)
    {
        if (TieneGestorPedidoAlimento(usuario))
            return "Precios";
        if (TieneGestorRecepcionHuevos(usuario))
            return "PreciosHuevo";
        if (TieneGestorVeterinario(usuario))
            return "ProgramasVacunacion";
        return "Precios";
    }

    private static bool TieneFuncionalidad(ClaimsPrincipal usuario, int bit) =>
        usuario.HasClaim(c => c.Type == ConstantesAutorizacion.ClaimRol
                && c.Value == ConstantesAutorizacion.RolGestorCaisy)
            && int.TryParse(
                usuario.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value,
                out var mascara)
            && (mascara & bit) == bit;
}
