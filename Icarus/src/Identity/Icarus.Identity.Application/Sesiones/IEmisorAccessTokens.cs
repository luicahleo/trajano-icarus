namespace Icarus.Identity.Application.Sesiones;

using Icarus.Identity.Domain;

public interface IEmisorAccessTokens
{
    string Emitir(
        Guid usuarioId, string rol, Guid? clienteId, Guid? trabajadorId,
        FuncionalidadesCaisy funcionalidadesCaisy, out int expiraEnSegundos);
}
