using System.Net;
using Serilog.Events;
using Trajano.GestorCaisy.Tests.Ayudas;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Observabilidad;

/// <summary>Privacidad de las excepciones MVC: el canario de la excepción y de
/// su inner no puede aparecer en ningún evento serializado de ningún sink ni
/// fuente, en Development, Testing ni Production, y aun así debe quedar un
/// diagnóstico seguro correlacionado (plan del cierre correctivo, tarea 1).</summary>
public sealed class PrivacidadExcepcionesTests
{
    private const string EventoResumen = "http.request.completed";
    private const string EventoBackend = "backend.error";
    private const string EventoFallback = "backend.error.fallback";

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task LaExcepcionCrudaNoLlegaANingunEventoSerializado(string entorno)
    {
        using var aplicacion = new AplicacionDePruebas { Entorno = entorno };
        var cliente = aplicacion.CreateClient();

        var respuesta = await cliente.GetAsync("/__pruebas/errores/lanzar");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var serializado = aplicacion.Colector.SerializarTodo();
        Assert.DoesNotContain(ControladorErroresDePrueba.Canario, serializado);
        Assert.DoesNotContain(ControladorErroresDePrueba.CanarioInterna, serializado);

        var backend = Assert.Single(Eventos(aplicacion, EventoBackend));
        Assert.Equal(LogEventLevel.Error, backend.Level);
        Assert.Null(backend.Exception);

        var resumen = Assert.Single(Eventos(aplicacion, EventoResumen));
        Assert.Equal("500", Prop(resumen, "StatusCode"));
        Assert.Equal(LogEventLevel.Error, resumen.Level);
        Assert.Null(resumen.Exception);
        Assert.Equal(Prop(backend, "ErrorId"), Prop(resumen, "ErrorId"));
    }

    [Fact]
    public async Task ElFalloDeLaPaginaDeErrorConservaReferenciaYSinCanario()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        await GetAsyncPermitiendoAborto(cliente, "/__pruebas/errores/pagina-error");

        AssertNoCanarios(aplicacion);
        var fallback = Assert.Single(Eventos(aplicacion, EventoFallback));
        Assert.Equal(LogEventLevel.Error, fallback.Level);
        Assert.Null(fallback.Exception);
        Assert.False(string.IsNullOrEmpty(Prop(fallback, "ErrorId")));
    }

    [Fact]
    public async Task LaRespuestaIniciadaAbortaSinFabricarOtraRespuesta()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        await GetAsyncPermitiendoAborto(cliente, "/__pruebas/errores/respuesta-iniciada");

        AssertNoCanarios(aplicacion);
        Assert.Single(Eventos(aplicacion, EventoFallback));
    }

    [Fact]
    public async Task LaCancelacionDelClienteNoFiltraLaExcepcion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        await GetAsyncPermitiendoAborto(cliente, "/__pruebas/errores/cancelacion");

        AssertNoCanarios(aplicacion);
    }

    [Fact]
    public async Task SiguenExistiendoEventosSegurosDeNivelError()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = aplicacion.CreateClient();

        await cliente.GetAsync("/__pruebas/errores/lanzar");

        Assert.Contains(aplicacion.Colector.Eventos,
            e => e.Level == LogEventLevel.Error && Prop(e, "EventName") == EventoBackend);
    }

    private static void AssertNoCanarios(AplicacionDePruebas aplicacion)
    {
        var serializado = aplicacion.Colector.SerializarTodo();
        Assert.DoesNotContain(ControladorErroresDePrueba.Canario, serializado);
        Assert.DoesNotContain(ControladorErroresDePrueba.CanarioInterna, serializado);
    }

    private static async Task GetAsyncPermitiendoAborto(HttpClient cliente, string ruta)
    {
        try
        {
            await cliente.GetAsync(ruta);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // La conexión se abortó a propósito: es la semántica esperada.
            _ = ex;
        }
    }

    private static List<LogEvent> Eventos(AplicacionDePruebas aplicacion, string nombre) =>
        aplicacion.Colector.Eventos
            .Where(e => Prop(e, "EventName") == nombre)
            .ToList();

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;
}
