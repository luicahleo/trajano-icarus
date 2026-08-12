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
        var existente = await _db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existente is null || existente.RevocadoEnUtc is not null || existente.ExpiraEnUtc <= DateTime.UtcNow)
            return null;

        existente.RevocadoEnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return existente.UsuarioId;
    }

    private static string Hashear(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
