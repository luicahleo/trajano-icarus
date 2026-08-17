using Icarus.Identity.Application.RegistroCuentas;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Icarus.Identity.Infrastructure.RegistroCuentas;

public sealed class RegistradorUsuarios : IRegistradorUsuarios
{
    private readonly UserManager<Usuario> _usuarios;
    private readonly ILogger<RegistradorUsuarios> _logger;

    public RegistradorUsuarios(UserManager<Usuario> usuarios, ILogger<RegistradorUsuarios> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<bool> EstaEmailRegistradoAsync(string email, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return await _usuarios.FindByEmailAsync(email) is not null;
    }

    public async Task<Guid?> RegistrarAsync(
        string email, string contrasena, string rol, Guid? clienteId, Guid? trabajadorId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (await EstaEmailRegistradoAsync(email, cancellationToken))
        {
            _logger.LogWarning("{EventName}: cuenta rechazada antes de crear la entidad", "identity.account_registration_rejected");
            return null;
        }

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
        if (!resultado.Succeeded)
            _logger.LogWarning("{EventName}: Identity rechazó la cuenta con códigos {IdentityErrorCodes}",
                "identity.account_registration_rejected", resultado.Errors.Select(error => error.Code).ToArray());
        return resultado.Succeeded ? usuario.Id : null;
    }
}
