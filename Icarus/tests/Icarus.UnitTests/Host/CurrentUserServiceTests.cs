using System.Security.Claims;
using Icarus.Host.Servicios;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Host;

public class CurrentUserServiceTests
{
    private static CurrentUserService CrearServicio(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext();
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba"));
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(contexto);
        return new CurrentUserService(accessor);
    }

    [Fact]
    public void UsuarioAutenticadoExponeSusClaims()
    {
        var usuarioId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var servicio = CrearServicio(
            new Claim("sub", usuarioId.ToString()),
            new Claim("rol", "Cliente"),
            new Claim("clienteId", clienteId.ToString()));

        Assert.True(servicio.EstaAutenticado);
        Assert.Equal(usuarioId, servicio.UsuarioId);
        Assert.Equal("Cliente", servicio.Rol);
        Assert.Equal(clienteId, servicio.ClienteId);
    }

    [Fact]
    public void UsuarioAutenticadoExponeTrabajadorId()
    {
        var trabajadorId = Guid.NewGuid();
        var servicio = CrearServicio(
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("rol", "Trabajador"),
            new Claim("trabajadorId", trabajadorId.ToString()));

        Assert.Equal(trabajadorId, servicio.TrabajadorId);
    }

    [Fact]
    public void SinTrabajadorIdDevuelveNull()
    {
        var servicio = CrearServicio(
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("rol", "Cliente"),
            new Claim("clienteId", Guid.NewGuid().ToString()));

        Assert.Null(servicio.TrabajadorId);
    }

    [Fact]
    public void SinClaimsDevuelveNulosYNoAutenticado()
    {
        var servicio = CrearServicio();
        Assert.False(servicio.EstaAutenticado);
        Assert.Null(servicio.UsuarioId);
        Assert.Null(servicio.ClienteId);
        Assert.Null(servicio.TrabajadorId);
    }
}
