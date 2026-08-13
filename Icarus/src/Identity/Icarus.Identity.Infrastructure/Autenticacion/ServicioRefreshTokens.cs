using System.Security.Cryptography;
using System.Text;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Icarus.Identity.Infrastructure.Autenticacion;

public sealed class ServicioRefreshTokens : IServicioRefreshTokens
{
    private readonly IdentityDbContext _db;
    private readonly OpcionesJwt _opciones;

    public ServicioRefreshTokens(IdentityDbContext db, IOptions<OpcionesJwt> opciones)
    {
        _db = db;
        _opciones = opciones.Value;
    }

    public async Task<string> EmitirAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            TokenHash = Hashear(token),
            ExpiraEnUtc = DateTime.UtcNow.AddDays(_opciones.DiasRefreshToken),
        });
        await _db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<Guid?> RotarAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hashear(refreshToken);
        var ahora = DateTime.UtcNow;

        // Revocación atómica: un solo UPDATE condicional evita la carrera entre
        // renovaciones concurrentes que presentan el mismo refresh token
        // (dos pestañas, StrictMode de React). Si no afecta filas, el token es
        // desconocido, ya está revocado o expiró: mismo resultado genérico.
        var revocados = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevocadoEnUtc == null && t.ExpiraEnUtc > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevocadoEnUtc, ahora), cancellationToken);
        if (revocados == 0)
            return null;

        // Esta llamada dejó la fila revocada: su UsuarioId es seguro de leer.
        return await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UsuarioId)
            .SingleAsync(cancellationToken);
    }

    private static string Hashear(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
