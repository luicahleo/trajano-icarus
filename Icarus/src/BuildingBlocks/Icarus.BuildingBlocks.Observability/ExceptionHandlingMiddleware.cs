using FluentValidation;
using Icarus.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Icarus.BuildingBlocks.Observability;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await EscribirProblemDetails(context, ex);
        }
    }

    private async Task EscribirProblemDetails(HttpContext context, Exception ex)
    {
        var (status, titulo) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };

        string? errorId = null;
        if (status >= StatusCodes.Status500InternalServerError)
        {
            errorId = DiagnosticIds.NuevoErrorId();
            DiagnosticContext.EstablecerErrorId(context, errorId);
            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["ErrorId"] = errorId,
                ["ExceptionType"] = ex.GetType().FullName,
                ["ExceptionStackTrace"] = ex.StackTrace,
            }))
            {
                _logger.LogError("{EventName}: error no controlado", "backend.error");
            }
        }
        else
        {
            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["ExceptionType"] = ex.GetType().FullName,
            }))
            {
                _logger.LogWarning("{EventName}: error esperado de negocio", "backend.business_warning");
            }
        }

        var correlationId = context.Items[CorrelationIdMiddleware.Header] as string
            ?? context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Instance = context.Request.Path,
        };
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = DiagnosticContext.ObtenerTraceId(context) ?? context.TraceIdentifier;
        if (errorId is not null) problem.Extensions["errorId"] = errorId;

        if (ex is ValidationException validacion)
        {
            problem.Extensions["errors"] = validacion.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
