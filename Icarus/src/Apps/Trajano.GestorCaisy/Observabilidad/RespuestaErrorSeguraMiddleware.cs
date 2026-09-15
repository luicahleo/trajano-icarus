using Serilog.Context;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Fallback terminal del pipeline de errores: cubre los caminos en que
/// <c>UseExceptionHandler</c> relanza (respuesta ya iniciada o fallo de la
/// propia página de error). Registra un diagnóstico propio seguro reutilizando
/// el ErrorId del incidente y aborta sin fabricar otra respuesta.</summary>
public sealed class RespuestaErrorSeguraMiddleware
{
    public const string EventoFallback = "backend.error.fallback";

    private readonly RequestDelegate _siguiente;
    private readonly ILogger<RespuestaErrorSeguraMiddleware> _registro;

    public RespuestaErrorSeguraMiddleware(
        RequestDelegate siguiente, ILogger<RespuestaErrorSeguraMiddleware> registro)
    {
        _siguiente = siguiente;
        _registro = registro;
    }

    public async Task Invoke(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (Exception ex)
        {
            RegistrarFallbackSeguro(contexto, ex);
            if (!contexto.Response.HasStarted)
                contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Abort();
        }
    }

    // El log vive fuera del catch a propósito: solo tipo, referencia y patrón
    // de ruta, nunca la excepción cruda con su stack.
    private void RegistrarFallbackSeguro(HttpContext contexto, Exception ex)
    {
        var errorId = contexto.Items.TryGetValue(ExcepcionesSegurasMiddleware.ErrorIdItem, out var valor)
            && valor is string texto
                ? texto
                : "ERR-DESCONOCIDO";
        var patron = RegistroHttpSeguro.PatronRuta(contexto);
        using (LogContext.PushProperty("ErrorId", errorId))
        using (LogContext.PushProperty("ExceptionType", ex.GetType().FullName))
        using (LogContext.PushProperty("RoutePattern", patron))
        using (LogContext.PushProperty("RequestPath", patron))
        {
            _registro.LogError(
                "{EventName}: la respuesta de error no pudo completarse", EventoFallback);
        }
    }
}
