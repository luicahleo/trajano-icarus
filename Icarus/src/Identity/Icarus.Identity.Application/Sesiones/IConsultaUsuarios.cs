namespace Icarus.Identity.Application.Sesiones;

public interface IConsultaUsuarios
{
    Task<UsuarioResumen?> ObtenerPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed record UsuarioResumen(Guid Id, string Email, string Rol, Guid? ClienteId);
