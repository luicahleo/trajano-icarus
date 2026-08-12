namespace Icarus.Identity.Application.Sesiones;

public interface IEmisorAccessTokens
{
    string Emitir(Guid usuarioId, string rol, Guid? clienteId, out int expiraEnSegundos);
}
