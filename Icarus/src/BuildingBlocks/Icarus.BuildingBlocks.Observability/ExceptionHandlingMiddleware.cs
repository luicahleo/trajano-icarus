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
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Error no controlado");
        else
            _logger.LogWarning(ex, "Error de negocio ({Tipo})", ex.GetType().Name);

        var correlationId = context.Items[CorrelationIdMiddleware.Header] as string
            ?? context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Instance = context.Request.Path,
        };
        problem.Extensions["correlationId"] = correlationId;

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
