using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.Identity.Infrastructure;

// Cuentas de prueba por rol, SOLO entornos dev/test (spec: sistema cerrado, sin
// rol Testing). Emails ficticios por anti-PII. ClienteDemoId es un placeholder:
// el módulo Clientes llega en el plan 3.
public static class SemillaIdentidad
{
    public static readonly Guid ClienteDemoId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TrabajadorDemoId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ClienteC1Id = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid TrabajadorT1Id = new("44444444-4444-4444-4444-444444444444");

    // Tenants exclusivos de Development (ver SembrarDesarrolloAsync).
    public static readonly Guid ClienteC2Id = new("55555555-5555-5555-5555-555555555555");
    public static readonly Guid TrabajadorT2Id = new("66666666-6666-6666-6666-666666666666");
    public static readonly Guid ClienteC3Id = new("77777777-7777-7777-7777-777777777777");
    public static readonly Guid TrabajadorT3Id = new("88888888-8888-8888-8888-888888888888");
    public static readonly Guid ClienteC4Id = new("99999999-9999-9999-9999-999999999999");
    public static readonly Guid TrabajadorT4Id = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public const string EmailAdmin = "admin@icarus.test";
    public const string EmailCliente = "cliente@icarus.test";
    public const string EmailTrabajador = "trabajador@icarus.test";
    public const string EmailClienteC1 = "c1@icarus.test";
    public const string EmailTrabajadorT1 = "t1@icarus.test";
    public const string EmailClienteC2 = "c2@icarus.test";
    public const string EmailTrabajadorT2 = "t2@icarus.test";
    public const string EmailClienteC3 = "c3@icarus.test";
    public const string EmailTrabajadorT3 = "t3@icarus.test";
    public const string EmailClienteC4 = "c4@icarus.test";
    public const string EmailTrabajadorT4 = "t4@icarus.test";

    // Cuentas de oficina de CAISY, solo Development: las mismas que
    // crear-usuario-caisy.ps1 y crear-usuario-gestor-recepcion-huevos.ps1
    // levantan a mano contra la VPS. En local ya no hace falta correrlos.
    public const string EmailGestorPedidoAlimento = "gpa@icarus.test";
    public const string EmailGestorRecepcionHuevos = "grh@icarus.test";

    public static async Task SembrarAsync(IServiceProvider servicios, string contrasenaPrueba)
    {
        var usuarios = servicios.GetRequiredService<UserManager<Usuario>>();
        await CrearSiNoExiste(usuarios, EmailAdmin, Rol.Administrador, null, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailCliente, Rol.Cliente, ClienteDemoId, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajador, Rol.Trabajador, ClienteDemoId, TrabajadorDemoId, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailClienteC1, Rol.Cliente, ClienteC1Id, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajadorT1, Rol.Trabajador, ClienteC1Id, TrabajadorT1Id, contrasenaPrueba);
    }

    // Tenants extra del escenario de desarrollo (spec
    // 2026-09-12-semilla-escenario-credito-huevo): SOLO bajo IsDevelopment().
    // No entran en Testing a propósito — la base de las pruebas de integración
    // es compartida y varias cuentan clientes y trabajadores, así que añadir
    // tenants ahí las rompería. Lo que Testing siembra es exactamente lo que
    // sembraba antes de este escenario.
    //
    // También reajusta la contraseña de las cuentas semilla ya creadas: la
    // creación es idempotente por existencia del email, así que una base local
    // anterior conservaría la contraseña vieja tras cambiar la configuración.
    public static async Task SembrarDesarrolloAsync(IServiceProvider servicios, string contrasenaPrueba)
    {
        var usuarios = servicios.GetRequiredService<UserManager<Usuario>>();
        await CrearSiNoExiste(usuarios, EmailClienteC2, Rol.Cliente, ClienteC2Id, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajadorT2, Rol.Trabajador, ClienteC2Id, TrabajadorT2Id, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailClienteC3, Rol.Cliente, ClienteC3Id, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajadorT3, Rol.Trabajador, ClienteC3Id, TrabajadorT3Id, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailClienteC4, Rol.Cliente, ClienteC4Id, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajadorT4, Rol.Trabajador, ClienteC4Id, TrabajadorT4Id, contrasenaPrueba);

        // Cuentas de oficina de CAISY: rol GestorCaisy sin tenant, con la
        // funcionalidad componible que corresponde a cada una.
        await CrearCuentaCaisySiNoExiste(usuarios, EmailGestorPedidoAlimento,
            FuncionalidadesCaisy.GestorPedidoAlimento, contrasenaPrueba);
        await CrearCuentaCaisySiNoExiste(usuarios, EmailGestorRecepcionHuevos,
            FuncionalidadesCaisy.GestorRecepcionHuevos, contrasenaPrueba);

        foreach (var email in new[]
        {
            EmailAdmin, EmailCliente, EmailTrabajador, EmailClienteC1, EmailTrabajadorT1,
            EmailClienteC2, EmailTrabajadorT2, EmailClienteC3, EmailTrabajadorT3,
            EmailClienteC4, EmailTrabajadorT4,
            EmailGestorPedidoAlimento, EmailGestorRecepcionHuevos,
        })
        {
            await AlinearContrasena(usuarios, email, contrasenaPrueba);
        }
    }

    private static async Task CrearCuentaCaisySiNoExiste(
        UserManager<Usuario> usuarios, string email,
        FuncionalidadesCaisy funcionalidades, string contrasena)
    {
        if (await usuarios.FindByEmailAsync(email) is { } existente)
        {
            // Idempotente, pero sí realinea las funcionalidades: si el
            // escenario cambia de función, la cuenta ya creada se actualiza en
            // vez de quedarse con la vieja.
            if (existente.FuncionalidadesCaisy != funcionalidades)
            {
                existente.FuncionalidadesCaisy = funcionalidades;
                await usuarios.UpdateAsync(existente);
            }
            return;
        }

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Rol = Rol.GestorCaisy.ToString(),
            ClienteId = null,
            TrabajadorId = null,
            Activo = true,
            FuncionalidadesCaisy = funcionalidades,
        };
        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
            throw new InvalidOperationException("No se pudo crear una cuenta semilla de CAISY.");
    }

    private static async Task AlinearContrasena(
        UserManager<Usuario> usuarios, string email, string contrasena)
    {
        if (await usuarios.FindByEmailAsync(email) is not { } usuario)
            return;
        if (await usuarios.CheckPasswordAsync(usuario, contrasena))
            return;
        await usuarios.RemovePasswordAsync(usuario);
        await usuarios.AddPasswordAsync(usuario, contrasena);
    }

    private static async Task CrearSiNoExiste(
        UserManager<Usuario> usuarios, string email, Rol rol,
        Guid? clienteId, Guid? trabajadorId, string contrasena)
    {
        if (await usuarios.FindByEmailAsync(email) is not null)
            return;

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Rol = rol.ToString(),
            ClienteId = clienteId,
            TrabajadorId = trabajadorId,
            Activo = true,
        };
        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
            throw new InvalidOperationException("No se pudo crear una cuenta semilla.");
    }
}
