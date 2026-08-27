using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Icarus.Identity.Infrastructure;

// Configuración opt-in del seed del administrador de plataforma en producción
// (paridad con Caserito). Solo se activa con Migraciones:EjecutarAlArranque y
// si SeedSettings está completo; los valores van por canal seguro / .env, nunca
// en git (anti-PII).
public sealed record OpcionesSeedAdmin
{
    public const string Seccion = "SeedSettings";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;

    internal bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(AdminEmail)
        && !string.IsNullOrWhiteSpace(AdminPassword);
}

// Crea el primer administrador de plataforma sin modificar usuarios existentes
// que ya tengan el rol. Idempotente: si el email ya existe con otro rol, se
// repara al rol Administrador. Los fallos de Identity no impiden el arranque.
public sealed partial class SeedAdminPlataforma(
    UserManager<Usuario> usuarios,
    ILogger<SeedAdminPlataforma> logger)
{
    public async Task EjecutarAsync(
        OpcionesSeedAdmin opciones,
        CancellationToken cancellationToken = default)
    {
        if (!opciones.EstaCompleta)
        {
            SeedOmitido(logger);
            return;
        }

        var existente = await usuarios.FindByEmailAsync(opciones.AdminEmail);
        if (existente is not null)
        {
            if (existente.Rol == Rol.Administrador.ToString())
            {
                SeedYaExistente(logger);
                return;
            }

            existente.Rol = Rol.Administrador.ToString();
            var reparado = await usuarios.UpdateAsync(existente);
            if (!reparado.Succeeded)
            {
                SeedFallo(logger, Codigos(reparado));
                return;
            }

            SeedRolReparado(logger);
            return;
        }

        var usuario = new Usuario
        {
            UserName = opciones.AdminEmail,
            Email = opciones.AdminEmail,
            EmailConfirmed = true,
            Rol = Rol.Administrador.ToString(),
            Activo = true,
        };

        var creado = await usuarios.CreateAsync(usuario, opciones.AdminPassword);
        if (!creado.Succeeded)
        {
            SeedFallo(logger, Codigos(creado));
            return;
        }

        SeedCreado(logger);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string Codigos(IdentityResult resultado)
        => string.Join(", ", resultado.Errors.Select(error => error.Code));

    [LoggerMessage(
        EventId = 1120,
        Level = LogLevel.Warning,
        Message = "Seed de admin omitido: configuración SeedSettings incompleta.")]
    private static partial void SeedOmitido(ILogger logger);

    [LoggerMessage(
        EventId = 1121,
        Level = LogLevel.Debug,
        Message = "Seed de admin omitido: el usuario ya existe con rol Administrador.")]
    private static partial void SeedYaExistente(ILogger logger);

    [LoggerMessage(
        EventId = 1122,
        Level = LogLevel.Error,
        Message = "Falló el seed de admin. Códigos Identity: {Codigos}.")]
    private static partial void SeedFallo(ILogger logger, string codigos);

    [LoggerMessage(
        EventId = 1123,
        Level = LogLevel.Information,
        Message = "Seed de admin creado con rol Administrador.")]
    private static partial void SeedCreado(ILogger logger);

    [LoggerMessage(
        EventId = 1124,
        Level = LogLevel.Information,
        Message = "Seed de admin reparado: rol Administrador asignado al usuario existente.")]
    private static partial void SeedRolReparado(ILogger logger);
}
