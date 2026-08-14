using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;

namespace Icarus.Identity.Infrastructure.Autenticacion;

public sealed class VerificadorCredenciales : IVerificadorCredenciales
{
    private readonly UserManager<Usuario> _usuarios;

    public VerificadorCredenciales(UserManager<Usuario> usuarios) => _usuarios = usuarios;

    public async Task<CredencialValida?> VerificarAsync(
        string email, string contrasena, CancellationToken cancellationToken = default)
    {
        // Anti-enumeración y anti-PII: un solo resultado nulo para email
        // inexistente, cuenta inactiva y contraseña incorrecta. El email y la
        // contraseña nunca se registran en logs.
        _ = cancellationToken;
        var usuario = await _usuarios.FindByEmailAsync(email);
        if (usuario is null || !usuario.Activo)
            return null;
        if (!await _usuarios.CheckPasswordAsync(usuario, contrasena))
            return null;
        return new CredencialValida(usuario.Id, usuario.Rol, usuario.ClienteId, usuario.TrabajadorId);
    }
}
