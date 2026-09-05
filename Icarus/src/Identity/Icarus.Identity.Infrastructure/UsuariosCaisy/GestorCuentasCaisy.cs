using Icarus.Identity.Application.UsuariosCaisy;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Icarus.Identity.Infrastructure.UsuariosCaisy;

// Cuentas CAISY (spec SP8): rol global GestorCaisy, sin tenant, con bitmask de
// funcionalidades. El correo y la contraseña nunca se registran en logs
// (anti-PII): solo códigos de error de Identity.
public sealed class GestorCuentasCaisy(UserManager<Usuario> usuarios, ILogger<GestorCuentasCaisy> logger)
    : ICuentasCaisy
{
    public async Task<Guid?> CrearAsync(
        string email, string contrasena, FuncionalidadesCaisy funcionalidades,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (await usuarios.FindByEmailAsync(email) is not null)
            return null;

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Rol = nameof(Rol.GestorCaisy),
            ClienteId = null,
            TrabajadorId = null,
            Activo = true,
            FuncionalidadesCaisy = funcionalidades,
        };
        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
        {
            logger.LogWarning("{EventName}: Identity rechazó la cuenta con códigos {IdentityErrorCodes}",
                "identity.caisy_account_rejected", resultado.Errors.Select(error => error.Code).ToArray());
            return null;
        }
        return usuario.Id;
    }

    public async Task<bool> DesactivarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
            return false;
        usuario.Activo = false;
        await usuarios.UpdateAsync(usuario);
        return true;
    }

    public async Task<bool> DefinirFuncionalidadesAsync(
        Guid usuarioId, FuncionalidadesCaisy funcionalidades, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo)
            return false;
        usuario.FuncionalidadesCaisy = funcionalidades;
        await usuarios.UpdateAsync(usuario);
        return true;
    }

    public async Task<IReadOnlyList<UsuarioCaisyResumen>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var filas = await usuarios.Users
            .Where(u => u.Rol == nameof(Rol.GestorCaisy))
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, Email = u.Email ?? string.Empty, u.Activo, u.FuncionalidadesCaisy })
            .ToListAsync(cancellationToken);
        return filas
            .Select(fila => new UsuarioCaisyResumen(
                fila.Id, fila.Email, fila.Activo, Nombres(fila.FuncionalidadesCaisy)))
            .ToList();
    }

    private static IReadOnlyList<string> Nombres(FuncionalidadesCaisy funcionalidades) =>
        Enum.GetValues<FuncionalidadesCaisy>()
            .Where(f => f is not FuncionalidadesCaisy.Ninguno && funcionalidades.HasFlag(f))
            .Select(f => f.ToString())
            .ToList();
}
