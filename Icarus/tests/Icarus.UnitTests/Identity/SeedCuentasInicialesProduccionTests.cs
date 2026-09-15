using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Identity;

// Bootstrap productivo (spec 2026-09-15-bootstrap-cuentas-produccion): con los
// cuatro secretos completos crea exactamente el administrador y las dos cuentas
// globales de CAISY; si falta cualquiera no crea nada; si ya existen alinea rol
// y funcionalidades sin tocar la contraseña.
public class SeedCuentasInicialesProduccionTests
{
    private static UserManager<Usuario> CrearUserManager() =>
        Substitute.For<UserManager<Usuario>>(
            Substitute.For<IUserStore<Usuario>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<Usuario>>(),
            Array.Empty<IUserValidator<Usuario>>(),
            Array.Empty<IPasswordValidator<Usuario>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<Usuario>>>());

    private static SeedCuentasInicialesProduccion CrearSeed(UserManager<Usuario> usuarios) =>
        new(usuarios, Substitute.For<ILogger<SeedCuentasInicialesProduccion>>());

    private static OpcionesSeedCuentasIniciales OpcionesCompletas() => new()
    {
        AdminEmail = "admin@icarus.online",
        AdminPassword = "Clave-Admin-123!",
        GestorRecepcionHuevosPassword = "Clave-Grh-123!",
        GestorPedidoAlimentoPassword = "Clave-Gpa-123!",
    };

    [Theory]
    [InlineData("AdminEmail")]
    [InlineData("AdminPassword")]
    [InlineData("GestorRecepcionHuevosPassword")]
    [InlineData("GestorPedidoAlimentoPassword")]
    public async Task FaltaDeCualquierSecretoOmiteElBootstrapCompleto(string secretoFaltante)
    {
        var usuarios = CrearUserManager();
        var opciones = secretoFaltante switch
        {
            "AdminEmail" => OpcionesCompletas() with { AdminEmail = string.Empty },
            "AdminPassword" => OpcionesCompletas() with { AdminPassword = string.Empty },
            "GestorRecepcionHuevosPassword" => OpcionesCompletas() with { GestorRecepcionHuevosPassword = string.Empty },
            _ => OpcionesCompletas() with { GestorPedidoAlimentoPassword = string.Empty },
        };

        await CrearSeed(usuarios).EjecutarAsync(opciones);

        await usuarios.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await usuarios.DidNotReceive().CreateAsync(Arg.Any<Usuario>(), Arg.Any<string>());
        await usuarios.DidNotReceive().UpdateAsync(Arg.Any<Usuario>());
    }

    [Fact]
    public async Task ConfiguracionCompletaCreaLasTresCuentasConRolYFuncionalidadExactos()
    {
        var usuarios = CrearUserManager();
        usuarios.CreateAsync(Arg.Any<Usuario>(), Arg.Any<string>())
            .Returns(Task.FromResult(IdentityResult.Success));

        var opciones = OpcionesCompletas();
        await CrearSeed(usuarios).EjecutarAsync(opciones);

        await usuarios.Received(1).CreateAsync(
            Arg.Is<Usuario>(u =>
                u.Email == opciones.AdminEmail
                && u.Rol == nameof(Rol.Administrador)
                && u.FuncionalidadesCaisy == FuncionalidadesCaisy.Ninguno
                && u.ClienteId == null
                && u.TrabajadorId == null
                && u.Activo),
            opciones.AdminPassword);
        await usuarios.Received(1).CreateAsync(
            Arg.Is<Usuario>(u =>
                u.Email == SeedCuentasInicialesProduccion.EmailGestorRecepcionHuevos
                && u.Rol == nameof(Rol.GestorCaisy)
                && u.FuncionalidadesCaisy == FuncionalidadesCaisy.GestorRecepcionHuevos
                && u.ClienteId == null
                && u.TrabajadorId == null
                && u.Activo),
            opciones.GestorRecepcionHuevosPassword);
        await usuarios.Received(1).CreateAsync(
            Arg.Is<Usuario>(u =>
                u.Email == SeedCuentasInicialesProduccion.EmailGestorPedidoAlimento
                && u.Rol == nameof(Rol.GestorCaisy)
                && u.FuncionalidadesCaisy == FuncionalidadesCaisy.GestorPedidoAlimento
                && u.ClienteId == null
                && u.TrabajadorId == null
                && u.Activo),
            opciones.GestorPedidoAlimentoPassword);
        await usuarios.Received(3).CreateAsync(Arg.Any<Usuario>(), Arg.Any<string>());
        await usuarios.DidNotReceive().UpdateAsync(Arg.Any<Usuario>());
    }

    [Fact]
    public async Task CuentasExistentesAlineanRolYFuncionalidadSinCambiarContrasena()
    {
        var usuarios = CrearUserManager();
        var opciones = OpcionesCompletas();

        var admin = new Usuario
        {
            Email = opciones.AdminEmail,
            Rol = nameof(Rol.Cliente),
            FuncionalidadesCaisy = FuncionalidadesCaisy.GestorPedidoAlimento,
        };
        var gestorRecepcion = new Usuario
        {
            Email = SeedCuentasInicialesProduccion.EmailGestorRecepcionHuevos,
            Rol = nameof(Rol.Trabajador),
            FuncionalidadesCaisy = FuncionalidadesCaisy.Ninguno,
        };
        var gestorPedido = new Usuario
        {
            Email = SeedCuentasInicialesProduccion.EmailGestorPedidoAlimento,
            Rol = nameof(Rol.Administrador),
            FuncionalidadesCaisy = FuncionalidadesCaisy.GestorRecepcionHuevos,
        };
        usuarios.FindByEmailAsync(opciones.AdminEmail).Returns(admin);
        usuarios.FindByEmailAsync(SeedCuentasInicialesProduccion.EmailGestorRecepcionHuevos).Returns(gestorRecepcion);
        usuarios.FindByEmailAsync(SeedCuentasInicialesProduccion.EmailGestorPedidoAlimento).Returns(gestorPedido);
        usuarios.UpdateAsync(Arg.Any<Usuario>()).Returns(Task.FromResult(IdentityResult.Success));

        await CrearSeed(usuarios).EjecutarAsync(opciones);

        Assert.Equal(nameof(Rol.Administrador), admin.Rol);
        Assert.Equal(FuncionalidadesCaisy.Ninguno, admin.FuncionalidadesCaisy);
        Assert.Equal(nameof(Rol.GestorCaisy), gestorRecepcion.Rol);
        Assert.Equal(FuncionalidadesCaisy.GestorRecepcionHuevos, gestorRecepcion.FuncionalidadesCaisy);
        Assert.Equal(nameof(Rol.GestorCaisy), gestorPedido.Rol);
        Assert.Equal(FuncionalidadesCaisy.GestorPedidoAlimento, gestorPedido.FuncionalidadesCaisy);

        await usuarios.Received(3).UpdateAsync(Arg.Any<Usuario>());
        await usuarios.DidNotReceive().CreateAsync(Arg.Any<Usuario>(), Arg.Any<string>());
        await usuarios.DidNotReceive().RemovePasswordAsync(Arg.Any<Usuario>());
        await usuarios.DidNotReceive().AddPasswordAsync(Arg.Any<Usuario>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CuentasYaAlineadasNoSeReescriben()
    {
        var usuarios = CrearUserManager();
        var opciones = OpcionesCompletas();

        usuarios.FindByEmailAsync(opciones.AdminEmail).Returns(new Usuario
        {
            Email = opciones.AdminEmail,
            Rol = nameof(Rol.Administrador),
            FuncionalidadesCaisy = FuncionalidadesCaisy.Ninguno,
        });
        usuarios.FindByEmailAsync(SeedCuentasInicialesProduccion.EmailGestorRecepcionHuevos).Returns(new Usuario
        {
            Email = SeedCuentasInicialesProduccion.EmailGestorRecepcionHuevos,
            Rol = nameof(Rol.GestorCaisy),
            FuncionalidadesCaisy = FuncionalidadesCaisy.GestorRecepcionHuevos,
        });
        usuarios.FindByEmailAsync(SeedCuentasInicialesProduccion.EmailGestorPedidoAlimento).Returns(new Usuario
        {
            Email = SeedCuentasInicialesProduccion.EmailGestorPedidoAlimento,
            Rol = nameof(Rol.GestorCaisy),
            FuncionalidadesCaisy = FuncionalidadesCaisy.GestorPedidoAlimento,
        });

        await CrearSeed(usuarios).EjecutarAsync(opciones);

        await usuarios.DidNotReceive().CreateAsync(Arg.Any<Usuario>(), Arg.Any<string>());
        await usuarios.DidNotReceive().UpdateAsync(Arg.Any<Usuario>());
    }
}
