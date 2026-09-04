using System.Security.Claims;
using Trajano.GestorCaisy.Autenticacion;

namespace Trajano.GestorCaisy.Autenticacion;

// Construcción del principal de sesión. Los tokens viajan como claims dentro
// de la cookie de autenticación protegida (DataProtection, HttpOnly): nunca en
// localStorage ni en HTML. El navegador solo recibe la cookie cifrada.
public static class PrincipalGestorcaisy
{
    public static ClaimsPrincipal? Crear(string accessToken, string refreshToken)
    {
        var encabezado = LectorTokenJwt.Leer(accessToken);
        if (encabezado is null)
            return null;
        var identidad = new ClaimsIdentity(
            "Trajano.GestorCaisy.Cookies", ConstantesAutorizacion.ClaimCorreo, null);
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimSub, encabezado.SujetoId));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimRol, encabezado.Rol));
        if (encabezado.FuncionalidadesCaisy is { } funcionalidades)
            identidad.AddClaim(new Claim(
                ConstantesAutorizacion.ClaimFuncionalidadesCaisy, funcionalidades.ToString()));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimAccessToken, accessToken));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimRefreshToken, refreshToken));
        return new ClaimsPrincipal(identidad);
    }

    // Al renovar la sesión se reemiten los claims con los tokens nuevos y se
    // conserva el resto (rol, funcionalidad, correo).
    public static ClaimsPrincipal ConTokensRenovados(
        ClaimsPrincipal actual, string accessToken, string refreshToken)
    {
        var original = actual.Identities.First();
        var identidad = new ClaimsIdentity(
            original.AuthenticationType, original.NameClaimType, original.RoleClaimType);
        foreach (var claim in original.Claims.Where(c =>
                c.Type is not (ConstantesAutorizacion.ClaimAccessToken
                    or ConstantesAutorizacion.ClaimRefreshToken)))
            identidad.AddClaim(claim);
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimAccessToken, accessToken));
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimRefreshToken, refreshToken));
        return new ClaimsPrincipal(identidad);
    }

    public static ClaimsPrincipal ConCorreo(ClaimsPrincipal principal, string correo)
    {
        var original = principal.Identities.First();
        var identidad = new ClaimsIdentity(
            original.AuthenticationType, original.NameClaimType, original.RoleClaimType);
        foreach (var claim in original.Claims)
            identidad.AddClaim(claim);
        identidad.AddClaim(new Claim(ConstantesAutorizacion.ClaimCorreo, correo));
        return new ClaimsPrincipal(identidad);
    }
}
