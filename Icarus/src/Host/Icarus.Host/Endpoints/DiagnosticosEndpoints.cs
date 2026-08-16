using Icarus.Host.Observability;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

public static partial class DiagnosticosEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticos(this IEndpointRouteBuilder app)
    {
        app.MapPost("/diagnosticos/frontend", Recibir)
            .Accepts<ReporteDiagnosticoFrontend>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("diagnosticos-frontend")
            .WithMetadata(new RequestSizeLimitAttribute(16_384));

        return app;
    }

    private static IResult Recibir(
        ReporteDiagnosticoFrontend reporte,
        ILogger<ReporteDiagnosticoFrontend> logger)
    {
        if (!ValidadorReporteDiagnosticoFrontend.EsValido(reporte))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Diagnóstico inválido");

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["ErrorId"] = reporte.ErrorId,
            ["ClientEventName"] = reporte.EventName,
            ["Category"] = reporte.Category,
            ["Source"] = reporte.Source,
            ["SessionId"] = reporte.SessionId,
            ["CorrelationId"] = reporte.CorrelationId,
            ["TraceId"] = reporte.TraceId,
            ["StatusCode"] = reporte.StatusCode,
            ["Release"] = reporte.Release,
            ["Asset"] = reporte.Asset,
            ["LineNumber"] = reporte.LineNumber,
            ["ColumnNumber"] = reporte.ColumnNumber,
        }))
        {
            LogFrontendError(logger);
        }

        if (reporte.FlowEvents is { } eventos)
        {
            foreach (var evento in eventos)
            {
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["SessionId"] = reporte.SessionId,
                    ["Seq"] = evento.Seq,
                    ["FlowEventName"] = evento.EventName,
                    ["Detail"] = evento.Detail,
                    ["CorrelationId"] = evento.CorrelationId,
                    ["TraceId"] = evento.TraceId,
                    ["StatusCode"] = evento.StatusCode,
                    ["DurationMs"] = evento.DurationMs,
                }))
                {
                    LogFrontendFlow(logger);
                }
            }
        }

        return Results.Accepted();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{EventName}: incidente técnico del navegador")]
    private static partial void LogFrontendError(
        ILogger logger,
        string eventName = "frontend.error");

    [LoggerMessage(Level = LogLevel.Information, Message = "{EventName}: evento de flujo del navegador")]
    private static partial void LogFrontendFlow(
        ILogger logger,
        string eventName = "frontend.flow");
}
