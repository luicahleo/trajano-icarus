using FluentValidation;
using Icarus.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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

    // Sombra el RequestPath concreto heredado del scope del framework con el
    // patrón de ruta resuelto; nunca copia el pathname recibido.
    private static Dictionary<string, object?> PropiedadesSeguras(
        HttpContext context, string? errorId, Exception ex)
    {
        var patron = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
            ?? "unmatched";
        var propiedades = new Dictionary<string, object?>
        {
            ["ExceptionType"] = ex.GetType().FullName,
            ["RoutePattern"] = patron,
            ["RequestPath"] = patron,
        };
        if (errorId is not null) propiedades["ErrorId"] = errorId;
        // El scope de identidad ya se desenrolló: se recupera de Items, que
        // persiste, para que el log de error exterior conserve el contexto.
        if (DiagnosticContext.ObtenerClienteId(context) is { } clienteId)
            propiedades["ClienteId"] = clienteId;
        if (DiagnosticContext.ObtenerRol(context) is { } rol)
            propiedades["Rol"] = rol;
        return propiedades;
    }

    private async Task EscribirProblemDetails(HttpContext context, Exception ex)
    {
        var (status, tituloGenerico) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acción no permitida"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };
        var titulo = ex is IExcepcionConTituloPropio conTituloPropio ? conTituloPropio.Titulo : tituloGenerico;

        string? errorId = null;
        if (status >= StatusCodes.Status500InternalServerError)
        {
            errorId = DiagnosticIds.NuevoErrorId();
            DiagnosticContext.EstablecerErrorId(context, errorId);
            // Solo tipo de excepción: el stack crudo puede arrastrar datos.
            using (_logger.BeginScope(PropiedadesSeguras(context, errorId, ex)))
            {
                _logger.LogError("{EventName}: error no controlado", "backend.error");
            }
        }
        else
        {
            using (_logger.BeginScope(PropiedadesSeguras(context, null, ex)))
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
