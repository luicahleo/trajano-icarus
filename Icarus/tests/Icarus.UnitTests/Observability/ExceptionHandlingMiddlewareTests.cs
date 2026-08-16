using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Icarus.UnitTests.Observability;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Cuerpo)> Ejecutar(Exception ex)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw ex,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.Invoke(contexto);

        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    [Fact]
    public async Task NotFoundExceptionDevuelve404()
    {
        var (status, _) = await Ejecutar(new NotFoundException("Cliente", Guid.NewGuid()));
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task ConflictExceptionDevuelve409()
    {
        var (status, _) = await Ejecutar(new ConflictException("conflicto"));
        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task ValidationExceptionDevuelve400ConErroresPorCampo()
    {
        var fallas = new[] { new ValidationFailure("Nombre", "obligatorio") };
        var (status, cuerpo) = await Ejecutar(new ValidationException(fallas));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("Nombre", out _));
    }

    [Fact]
    public async Task UnauthorizedAccessExceptionDevuelve401SinDetalle()
    {
        var (status, cuerpo) = await Ejecutar(new UnauthorizedAccessException("detalle interno sensible"));
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.DoesNotContain("detalle interno sensible", cuerpo.ToString());
    }

    [Fact]
    public async Task ExcepcionNoControladaDevuelve500GenericoSinDetalleTecnico()
    {
        var (status, cuerpo) = await Ejecutar(new InvalidOperationException("detalle interno sensible"));
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("detalle interno sensible", cuerpo.ToString());
        Assert.Matches("^ERR-[0-9A-F]{12}$", cuerpo.GetProperty("errorId").GetString());
        Assert.True(cuerpo.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task ReglaNegocioExceptionDevuelve400()
    {
        var (status, cuerpo) = await Ejecutar(new ReglaNegocioException("regla violada"));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.DoesNotContain("regla violada", cuerpo.ToString());
    }

    [Fact]
    public async Task TodaRespuestaIncluyeCorrelationId()
    {
        var (_, cuerpo) = await Ejecutar(new ConflictException("conflicto"));
        Assert.True(cuerpo.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task ErrorEsperadoNoRecibeReferenciaDeIncidenteTecnico()
    {
        var (_, cuerpo) = await Ejecutar(new ConflictException("conflicto"));

        Assert.False(cuerpo.TryGetProperty("errorId", out _));
    }
}
