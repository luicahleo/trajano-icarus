using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Icarus.BuildingBlocks.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";
    private const int LongitudMaxima = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var entrante = context.Request.Headers[Header].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(entrante) || entrante.Length > LongitudMaxima
            ? Guid.NewGuid().ToString()
            : entrante;

        context.Items[Header] = correlationId;
        context.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
