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

    public const string EmailAdmin = "admin@icarus.test";
    public const string EmailCliente = "cliente@icarus.test";
    public const string EmailTrabajador = "trabajador@icarus.test";
    public const string EmailClienteC1 = "c1@icarus.test";
    public const string EmailTrabajadorT1 = "t1@icarus.test";

    public static async Task SembrarAsync(IServiceProvider servicios, string contrasenaPrueba)
    {
        var usuarios = servicios.GetRequiredService<UserManager<Usuario>>();
        await CrearSiNoExiste(usuarios, EmailAdmin, Rol.Administrador, null, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailCliente, Rol.Cliente, ClienteDemoId, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajador, Rol.Trabajador, ClienteDemoId, TrabajadorDemoId, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailClienteC1, Rol.Cliente, ClienteC1Id, null, contrasenaPrueba);
        await CrearSiNoExiste(usuarios, EmailTrabajadorT1, Rol.Trabajador, ClienteC1Id, TrabajadorT1Id, contrasenaPrueba);
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
