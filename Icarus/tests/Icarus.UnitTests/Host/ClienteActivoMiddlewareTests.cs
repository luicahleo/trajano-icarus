using Icarus.BuildingBlocks.Application;
using Icarus.Host.Middleware;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Icarus.UnitTests.Host;

public class ClienteActivoMiddlewareTests
{
    [Fact]
    public async Task TrabajadorSinClienteEsRechazadoAunqueElTokenSeaValido()
    {
        var siguiente = Substitute.For<RequestDelegate>();
        var usuario = Substitute.For<ICurrentUser>();
        var estado = Substitute.For<IClienteActivo>();
        var contexto = new DefaultHttpContext();
        usuario.EstaAutenticado.Returns(true);
        usuario.Rol.Returns("Trabajador");
        usuario.ClienteId.Returns((Guid?)null);

        var middleware = new ClienteActivoMiddleware(siguiente);

        await middleware.InvokeAsync(contexto, usuario, estado);

        Assert.Equal(StatusCodes.Status401Unauthorized, contexto.Response.StatusCode);
        await siguiente.DidNotReceive().Invoke(Arg.Any<HttpContext>());
    }
}
