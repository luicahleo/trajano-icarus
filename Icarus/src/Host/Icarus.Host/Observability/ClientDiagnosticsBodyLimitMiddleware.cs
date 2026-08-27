namespace Icarus.Host.Observability;

public sealed class ClientDiagnosticsBodyLimitMiddleware(RequestDelegate next)
{
    private const long MaximoBytes = 16_384;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/api/diagnosticos/frontend")
            && context.Request.ContentLength > MaximoBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await next(context);
    }
}
