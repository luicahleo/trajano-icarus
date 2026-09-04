using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Sesion;

// Implementación sobre la cookie de autenticación protegida: los claims se
// leen del principal de la petición y la renovación reemite la cookie cifrada
// con los tokens nuevos (HttpOnly, nunca localStorage).
public sealed class SesionCaisyCookie(IHttpContextAccessor accessor) : ISesionCaisyActual
{
    public string? AccessToken =>
        accessor.HttpContext?.User.FindFirst(ConstantesAutorizacion.ClaimAccessToken)?.Value;

    public string? RefreshToken =>
        accessor.HttpContext?.User.FindFirst(ConstantesAutorizacion.ClaimRefreshToken)?.Value;

    public async Task RenovarTokensAsync(
        string accessToken, string refreshToken, CancellationToken token = default)
    {
        var contexto = accessor.HttpContext
            ?? throw new InvalidOperationException(
                "No hay petición en curso para renovar la sesión.");
        var renovado = PrincipalGestorcaisy.ConTokensRenovados(
            contexto.User, accessToken, refreshToken);
        await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, renovado);
    }
}
