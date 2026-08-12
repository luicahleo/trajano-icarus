using Icarus.Identity.Application.Usuarios;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;

namespace Icarus.Identity.Infrastructure.Usuarios;

public sealed class RegistradorUsuarios : IRegistradorUsuarios
{
    private readonly UserManager<Usuario> _usuarios;

    public RegistradorUsuarios(UserManager<Usuario> usuarios) => _usuarios = usuarios;

    public async Task<Guid?> RegistrarAsync(
        string email, string contrasena, string rol, Guid? clienteId, Guid? trabajadorId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (await _usuarios.FindByEmailAsync(email) is not null)
            return null;

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Rol = rol,
            ClienteId = clienteId,
            TrabajadorId = trabajadorId,
            Activo = true,
        };
        var resultado = await _usuarios.CreateAsync(usuario, contrasena);
        return resultado.Succeeded ? usuario.Id : null;
    }
}
