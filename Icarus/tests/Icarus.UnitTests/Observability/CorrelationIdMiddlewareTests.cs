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
        const string correlationId = "20cc2ea2-2f71-45bb-a667-25f1700431bb";
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = correlationId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        Assert.Equal(correlationId, contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString());
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

    [Fact]
    public async Task HeaderEntranteQueNoEsUuidSeReemplaza()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = "texto-arbitrario";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        Assert.True(Guid.TryParse(
            contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString(), out _));
    }
}
