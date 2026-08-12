namespace Icarus.Identity.Application.Sesiones;

public interface IServicioRefreshTokens
{
    Task<string> EmitirAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    // Rotación: el token presentado queda revocado; null si no existe, está
    // revocado o expiró.
    Task<Guid?> RotarAsync(string refreshToken, CancellationToken cancellationToken = default);
}
