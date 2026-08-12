namespace Icarus.Identity.Infrastructure.Persistencia;

// Anti-PII: solo se persiste el hash SHA-256 del token, nunca el token en claro.
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEnUtc { get; set; }
    public DateTime? RevocadoEnUtc { get; set; }
}
