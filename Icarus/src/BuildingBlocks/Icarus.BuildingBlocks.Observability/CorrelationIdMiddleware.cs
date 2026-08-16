using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Icarus.BuildingBlocks.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var entrante = context.Request.Headers[Header].FirstOrDefault();
        var correlationId = Guid.TryParse(entrante, out var recibido)
            ? recibido.ToString()
            : Guid.NewGuid().ToString();

        context.Items[Header] = correlationId;
        context.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
