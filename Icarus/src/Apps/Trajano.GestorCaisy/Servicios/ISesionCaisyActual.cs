namespace Trajano.GestorCaisy.Servicios;

// Vista de la aplicación sobre la sesión actual: los tokens viven como claims
// de la cookie de autenticación protegida y solo este servidor los lee.
public interface ISesionCaisyActual
{
    string? AccessToken { get; }

    string? RefreshToken { get; }

    // Renovación confirmada por la API: reemite la cookie con los tokens nuevos.
    Task RenovarTokensAsync(string accessToken, string refreshToken, CancellationToken token = default);
}
