using Icarus.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Icarus.UnitTests.Observability;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task SinHeaderGeneraUnCorrelationIdYLoExponeEnLaRespuesta()
    {
        var contexto = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        var id = contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString();
        Assert.True(Guid.TryParse(id, out _), $"ID inesperado: {id}");
    }

    [Fact]
    public async Task ConHeaderEntranteLoPropagaSinCambiarlo()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = "abc123";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        Assert.Equal("abc123", contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString());
    }

    [Fact]
    public async Task HeaderEntranteDemasiadoLargoSeReemplaza()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = new string('x', 100);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        var id = contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString();
        Assert.True(Guid.TryParse(id, out _));
    }
}
