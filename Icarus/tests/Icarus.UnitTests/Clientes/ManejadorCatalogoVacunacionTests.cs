using System.Security.Claims;
using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class ManejadorCatalogoVacunacionTests
{
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly IVerificadorEntitlement _entitlement = Substitute.For<IVerificadorEntitlement>();

    private async Task<bool> Autoriza()
    {
        var requisito = new RequisitoCatalogoVacunacion();
        var contexto = new AuthorizationHandlerContext([requisito], new ClaimsPrincipal(), null);
        await new ManejadorCatalogoVacunacion(_usuario, _entitlement).HandleAsync(contexto);
        return contexto.HasSucceeded;
    }

    [Fact]
    public async Task AdministradorSinClientePasa()
    {
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Administrador");

        Assert.True(await Autoriza());
    }

    [Fact]
    public async Task ClienteConElModuloPasa()
    {
        var clienteId = Guid.NewGuid();
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Cliente");
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        _entitlement.TieneFuncionalidadAsync(clienteId, null, Funcionalidades.Vacunacion, Arg.Any<CancellationToken>())
            .Returns(true);

        Assert.True(await Autoriza());
    }

    [Fact]
    public async Task TrabajadorSinLaFuncionalidadNoPasa()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        _usuario.EstaAutenticado.Returns(true);
        _usuario.Rol.Returns("Trabajador");
        _usuario.ClienteId.Returns<Guid?>(clienteId);
        _usuario.TrabajadorId.Returns<Guid?>(trabajadorId);
        _entitlement.TieneFuncionalidadAsync(clienteId, trabajadorId, Funcionalidades.Vacunacion, Arg.Any<CancellationToken>())
            .Returns(false);

        Assert.False(await Autoriza());
    }

    [Fact]
    public async Task NoAutenticadoNoPasa()
    {
        _usuario.EstaAutenticado.Returns(false);

        Assert.False(await Autoriza());
    }
}
