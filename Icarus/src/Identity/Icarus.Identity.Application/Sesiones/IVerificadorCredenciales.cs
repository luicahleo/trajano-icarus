namespace Icarus.Identity.Application.Sesiones;

using Icarus.Identity.Domain;

// Devuelve null ante email inexistente, cuenta inactiva o contraseña
// incorrecta: el caller no puede distinguir los casos (anti-enumeración).
public interface IVerificadorCredenciales
{
    Task<CredencialValida?> VerificarAsync(
        string email, string contrasena, CancellationToken cancellationToken = default);
}

public sealed record CredencialValida(
    Guid UsuarioId, string Rol, Guid? ClienteId, Guid? TrabajadorId,
    FuncionalidadesCaisy FuncionalidadesCaisy);
