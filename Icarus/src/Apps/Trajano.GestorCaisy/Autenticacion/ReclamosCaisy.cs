using System.Security.Claims;

namespace Trajano.GestorCaisy.Autenticacion;

// Comprobación compartida de rol y funcionalidad: la usa la política de
// autorización y el menú de la barra lateral (un menú limitado por función no
// sustituye la autorización del servidor, solo la complementa).
public static class ReclamosCaisy
{
    public static bool TieneGestorPedidoAlimento(ClaimsPrincipal usuario) =>
        usuario.HasClaim(c => c.Type == ConstantesAutorizacion.ClaimRol
                && c.Value == ConstantesAutorizacion.RolGestorCaisy)
            && int.TryParse(
                usuario.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value,
                out var mascara)
            && (mascara & ConstantesAutorizacion.BitGestorPedidoAlimento)
                == ConstantesAutorizacion.BitGestorPedidoAlimento;
}
