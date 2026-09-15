using System.Security.Cryptography;
using Serilog.Context;

namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Registra una referencia técnica del error no controlado sin stack ni
/// datos crudos y lo vuelve a lanzar para que el manejador de errores lo
/// consuma dentro del resumen HTTP.</summary>
public sealed class ExcepcionesSegurasMiddleware
{
    public const string ErrorIdItem = "Trajano.Observabilidad.ErrorId";

    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ExcepcionesSegurasMiddleware> _registro;

    public ExcepcionesSegurasMiddleware(
        RequestDelegate siguiente, ILogger<ExcepcionesSegurasMiddleware> registro)
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
            RegistrarErrorSeguro(contexto, ex);
            throw;
        }
    }

    // El log vive fuera del catch a propósito: se registra el tipo de excepción
    // y una referencia técnica, nunca la excepción cruda con su stack.
    private void RegistrarErrorSeguro(HttpContext contexto, Exception ex)
    {
        // Un fallo secundario al representar la página reutiliza la referencia
        // del incidente original en lugar de sustituirla por otra.
        var errorId = contexto.Items.TryGetValue(ErrorIdItem, out var previo)
            && previo is string previoTexto
                ? previoTexto
                : $"ERR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";
        contexto.Items[ErrorIdItem] = errorId;
        var patron = RegistroHttpSeguro.PatronRuta(contexto);
        using (LogContext.PushProperty("ErrorId", errorId))
        using (LogContext.PushProperty("ExceptionType", ex.GetType().FullName))
        using (LogContext.PushProperty("RoutePattern", patron))
        using (LogContext.PushProperty("RequestPath", patron))
        {
            _registro.LogError("{EventName}: error no controlado", "backend.error");
        }
    }
}
