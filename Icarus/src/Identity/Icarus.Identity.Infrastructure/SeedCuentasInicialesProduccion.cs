using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Icarus.Identity.Infrastructure;

// Configuración opt-in del bootstrap productivo de cuentas globales (paridad
// con Caserito). Solo se activa con Migraciones:EjecutarAlArranque y si
// SeedSettings está completo; los valores van por canal seguro / .env, nunca en
// git (anti-PII). Las tres contraseñas y el correo del administrador son
// obligatorios: si falta uno, no se crea ninguna cuenta.
public sealed record OpcionesSeedCuentasIniciales
{
    public const string Seccion = "SeedSettings";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string GestorRecepcionHuevosPassword { get; set; } = string.Empty;
    public string GestorPedidoAlimentoPassword { get; set; } = string.Empty;

    internal bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(AdminEmail)
        && !string.IsNullOrWhiteSpace(AdminPassword)
        && !string.IsNullOrWhiteSpace(GestorRecepcionHuevosPassword)
        && !string.IsNullOrWhiteSpace(GestorPedidoAlimentoPassword);
}

// Bootstrap idempotente de las cuentas globales mínimas de producción: el
// administrador configurado y las dos cuentas de oficina de CAISY. Nunca crea
// ni borra datos de dominio (clientes, trabajadores, granjas, galpones,
// pedidos). Si una cuenta ya existe, corrige rol y funcionalidades al estado
// definido, pero no cambia su contraseña. Los logs solo llevan el tipo de
// cuenta y códigos técnicos de Identity; jamás correos ni credenciales.
public sealed partial class SeedCuentasInicialesProduccion(
    UserManager<Usuario> usuarios,
    ILogger<SeedCuentasInicialesProduccion> logger)
{
    public const string EmailGestorRecepcionHuevos = "grh@icarus.online";
    public const string EmailGestorPedidoAlimento = "gpa@icarus.online";

    private const string TipoAdministrador = "administrador";
    private const string TipoGestorRecepcionHuevos = "gestor-recepcion-huevos";
    private const string TipoGestorPedidoAlimento = "gestor-pedido-alimento";

    public async Task EjecutarAsync(
        OpcionesSeedCuentasIniciales opciones,
        CancellationToken cancellationToken = default)
    {
        if (!opciones.EstaCompleta)
        {
            SeedOmitido(logger);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await SembrarAdministradorAsync(opciones.AdminEmail, opciones.AdminPassword, cancellationToken);
        await SembrarCuentaCaisyAsync(
            EmailGestorRecepcionHuevos, opciones.GestorRecepcionHuevosPassword,
            FuncionalidadesCaisy.GestorRecepcionHuevos, TipoGestorRecepcionHuevos, cancellationToken);
        await SembrarCuentaCaisyAsync(
            EmailGestorPedidoAlimento, opciones.GestorPedidoAlimentoPassword,
            FuncionalidadesCaisy.GestorPedidoAlimento, TipoGestorPedidoAlimento, cancellationToken);
    }

    private async Task SembrarAdministradorAsync(
        string email, string contrasena, CancellationToken cancellationToken)
    {
        if (await usuarios.FindByEmailAsync(email) is { } existente)
        {
            await AlinearAsync(
                existente, Rol.Administrador, FuncionalidadesCaisy.Ninguno,
                TipoAdministrador, cancellationToken);
            return;
        }

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Rol = nameof(Rol.Administrador),
            Activo = true,
            FuncionalidadesCaisy = FuncionalidadesCaisy.Ninguno,
        };

        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
        {
            SeedFallo(logger, TipoAdministrador, Codigos(resultado));
            return;
        }

        SeedCreado(logger, TipoAdministrador);
    }

    private async Task SembrarCuentaCaisyAsync(
        string email, string contrasena, FuncionalidadesCaisy funcionalidades,
        string tipoCuenta, CancellationToken cancellationToken)
    {
        if (await usuarios.FindByEmailAsync(email) is { } existente)
        {
            await AlinearAsync(existente, Rol.GestorCaisy, funcionalidades, tipoCuenta, cancellationToken);
            return;
        }

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Rol = nameof(Rol.GestorCaisy),
            ClienteId = null,
            TrabajadorId = null,
            Activo = true,
            FuncionalidadesCaisy = funcionalidades,
        };

        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
        {
            SeedFallo(logger, tipoCuenta, Codigos(resultado));
            return;
        }

        SeedCreado(logger, tipoCuenta);
    }

    private async Task AlinearAsync(
        Usuario usuario, Rol rol, FuncionalidadesCaisy funcionalidades,
        string tipoCuenta, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rolTexto = rol.ToString();
        if (usuario.Rol == rolTexto && usuario.FuncionalidadesCaisy == funcionalidades)
        {
            SeedYaAlineado(logger, tipoCuenta);
            return;
        }

        usuario.Rol = rolTexto;
        usuario.FuncionalidadesCaisy = funcionalidades;
        var resultado = await usuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            SeedFallo(logger, tipoCuenta, Codigos(resultado));
            return;
        }

        SeedAlineado(logger, tipoCuenta);
    }

    private static string Codigos(IdentityResult resultado)
        => string.Join(", ", resultado.Errors.Select(error => error.Code));

    [LoggerMessage(
        EventId = 1120,
        Level = LogLevel.Warning,
        Message = "Bootstrap de cuentas iniciales omitido: configuración SeedSettings incompleta.")]
    private static partial void SeedOmitido(ILogger logger);

    [LoggerMessage(
        EventId = 1121,
        Level = LogLevel.Information,
        Message = "Bootstrap: cuenta {TipoCuenta} creada.")]
    private static partial void SeedCreado(ILogger logger, string tipoCuenta);

    [LoggerMessage(
        EventId = 1122,
        Level = LogLevel.Error,
        Message = "Falló el bootstrap de la cuenta {TipoCuenta}. Códigos Identity: {Codigos}.")]
    private static partial void SeedFallo(ILogger logger, string tipoCuenta, string codigos);

    [LoggerMessage(
        EventId = 1123,
        Level = LogLevel.Information,
        Message = "Bootstrap: cuenta {TipoCuenta} alineada a su rol y funcionalidad definidos.")]
    private static partial void SeedAlineado(ILogger logger, string tipoCuenta);

    [LoggerMessage(
        EventId = 1124,
        Level = LogLevel.Debug,
        Message = "Bootstrap: cuenta {TipoCuenta} ya estaba alineada.")]
    private static partial void SeedYaAlineado(ILogger logger, string tipoCuenta);
}
