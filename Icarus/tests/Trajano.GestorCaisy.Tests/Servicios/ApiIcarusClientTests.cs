using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Servicios;

public class ApiIcarusClientTests
{
    private const string BaseApi = "http://api.icarus.test/api/";

    private readonly FakeManejadorHttp _manejador = new();
    private readonly ISesionCaisyActual _sesion = Substitute.For<ISesionCaisyActual>();
    private readonly ApiIcarusClient _cliente;

    public ApiIcarusClientTests()
    {
        _sesion.AccessToken.Returns("token-actual");
        _sesion.RefreshToken.Returns("refresh-actual");
        _sesion.RenovarTokensAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _sesion.AccessToken.Returns(ci.ArgAt<string>(0));
                _sesion.RefreshToken.Returns(ci.ArgAt<string>(1));
                return Task.CompletedTask;
            });
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiIcarus:BaseUrl"] = BaseApi })
            .Build();
        _cliente = new ApiIcarusClient(
            new HttpClient(_manejador), configuracion, new HttpContextAccessor(), _sesion,
            NullLogger<ApiIcarusClient>.Instance);
    }

    [Fact]
    public async Task IniciarSesionPosteaCredencialesYParseaElAccessToken()
    {
        _manejador.Responder(HttpStatusCode.OK,
            """{"accessToken":"access.abc.def","expiraEnSegundos":900}""",
            [new("Set-Cookie", "icarus_refresh=refresh-123; Path=/; HttpOnly")]);

        var sesion = await _cliente.IniciarSesionAsync("gestor@caisy.test", "Clave-123");

        Assert.Equal("access.abc.def", sesion.AccessToken);
        Assert.Equal(900, sesion.ExpiraEnSegundos);
        Assert.Equal("refresh-123", sesion.RefreshToken);
        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}identidad/sesion", peticion.Uri.ToString());
        Assert.Contains("\"email\":\"gestor@caisy.test\"", peticion.Cuerpo);
        Assert.Contains("\"contrasena\":\"Clave-123\"", peticion.Cuerpo);
        Assert.Null(peticion.Autorizacion);
    }

    [Fact]
    public async Task CredencialesInvalidasLanzaErrorApiExceptionConEstado401()
    {
        _manejador.Responder(HttpStatusCode.Unauthorized,
            """{"title":"No autorizado","status":401}""");

        var error = await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.IniciarSesionAsync("gestor@caisy.test", "clave-erronea"));

        Assert.Equal(401, error.Estado);
        Assert.Equal("No autorizado", error.Titulo);
    }

    [Fact]
    public async Task Un401DeSesionNoDisparaLaRenovacion()
    {
        _manejador.Responder(HttpStatusCode.Unauthorized, """{"title":"No autorizado"}""");

        await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.IniciarSesionAsync("gestor@caisy.test", "clave-erronea"));

        Assert.Single(_manejador.Peticiones);
    }

    [Fact]
    public async Task ErroresDeValidacionSeTransportanAlLlamador()
    {
        _manejador.Responder(HttpStatusCode.BadRequest,
            """{"title":"Solicitud inválida","errors":{"Documento":["Fila 3: precio ilegible"]}}""");

        var error = await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.IniciarSesionAsync("gestor@caisy.test", "clave"));

        Assert.Equal(400, error.Estado);
        Assert.Equal("Fila 3: precio ilegible",
            error.ErroresValidacion!["Documento"][0]);
    }

    [Fact]
    public async Task DespacharPosteaNotaYLineasEnLaRutaDelPedido()
    {
        _manejador.Responder(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        await _cliente.DespacharPedidoAsync(new ComandoDespachoApi(
            id, "NOTA-77", new(2025, 12, 1), 14120m,
            [new LineaDespachoApi("PosturaUno", 75)]));

        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}pedidos-alimento-caisy/{id}/despachar", peticion.Uri.ToString());
        Assert.Equal("Bearer token-actual", peticion.Autorizacion);
        Assert.Contains("\"numeroNota\":\"NOTA-77\"", peticion.Cuerpo);
        Assert.Contains("\"cantidadEntregada\":75", peticion.Cuerpo);
    }

    [Fact]
    public async Task SubirDocumentoNotaEnviaMultipartYReemplazo()
    {
        _manejador.Responder(HttpStatusCode.Created, """{"id":"9b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c009"}""");
        var id = Guid.NewGuid();
        var previo = Guid.NewGuid();

        var documentoId = await _cliente.SubirDocumentoNotaAsync(
            id, new MemoryStream([1, 2, 3]), "nota-frente.png", previo);

        Assert.Equal(Guid.Parse("9b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c009"), documentoId);
        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}pedidos-alimento-caisy/{id}/nota/documentos", peticion.Uri.ToString());
        Assert.Contains("reemplazaDocumentoId", peticion.Cuerpo);
    }

    [Fact]
    public async Task DescargarDocumentoNotaDevuelveElContenidoYElTipo()
    {
        _manejador.Responder(HttpStatusCode.OK, contenido: [0xFF, 0xD8, 0xFF, 1], tipoDeContenido: "image/jpeg");
        var id = Guid.NewGuid();
        var documentoId = Guid.NewGuid();

        var (contenido, tipo) = await _cliente.DescargarDocumentoNotaAsync(id, documentoId);

        Assert.Equal("image/jpeg", tipo);
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria);
        Assert.Equal([0xFF, 0xD8, 0xFF, 1], memoria.ToArray());
        Assert.Equal($"{BaseApi}pedidos-alimento-caisy/{id}/nota/documentos/{documentoId}/vista",
            _manejador.Peticiones[0].Uri.ToString());
    }

    [Fact]
    public async Task ListarNotificacionesEnviaBearerYParseaLaColeccion()
    {
        _manejador.Responder(HttpStatusCode.OK,
            """[{"id":"6b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c001","fechaDocumento":"2025-11-02","vigenteDesde":"2025-12-01","estado":"Publicada","cantidadDetalles":12,"tieneDocumentoOriginal":true}]""");

        var lista = await _cliente.ListarNotificacionesAsync();

        var resumen = Assert.Single(lista);
        Assert.Equal("Publicada", resumen.Estado);
        Assert.Equal(12, resumen.CantidadDetalles);
        Assert.True(resumen.TieneDocumentoOriginal);
        Assert.Equal(new DateOnly(2025, 11, 2), resumen.FechaDocumento);
        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Get, peticion.Metodo);
        Assert.Equal($"{BaseApi}precios-alimentos/", peticion.Uri.ToString());
        Assert.Equal("Bearer token-actual", peticion.Autorizacion);
    }

    [Fact]
    public async Task ActualizarBorradorEnviaJsonCamelCaseConEnumsComoNombres()
    {
        _manejador.Responder(HttpStatusCode.NoContent);
        var comando = new ComandoActualizarBorradorApi(
            Guid.Parse("6b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c001"),
            new(2025, 11, 2), new(2025, 12, 1), 1.20m, 0.60m, 0.75m,
            [new DatosDetalleApi("Preiniciador", "Bolsa", 118.50m, 1, 21, 115.00m)]);

        await _cliente.ActualizarBorradorAsync(comando);

        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Put, peticion.Metodo);
        Assert.Equal(
            $"{BaseApi}precios-alimentos/6b2e4c46-2f1a-4b7e-9b4b-6ee7f7f2c001",
            peticion.Uri.ToString());
        Assert.Contains("\"notificacionId\"", peticion.Cuerpo);
        Assert.Contains("\"fechaDocumento\":\"2025-11-02\"", peticion.Cuerpo);
        Assert.Contains("\"vigenteDesde\":\"2025-12-01\"", peticion.Cuerpo);
        Assert.Contains("\"tipoAlimento\":\"Preiniciador\"", peticion.Cuerpo);
        Assert.Contains("\"presentacion\":\"Bolsa\"", peticion.Cuerpo);
        Assert.Contains("\"precioFinalPor40Kg\":118.50", peticion.Cuerpo);
        Assert.Contains("\"precioActualDocumento\":115.00", peticion.Cuerpo);
    }

    [Fact]
    public async Task PublicarPosteaALaRutaDePublicacion()
    {
        _manejador.Responder(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        await _cliente.PublicarAsync(id);

        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}precios-alimentos/{id}/publicar", peticion.Uri.ToString());
    }

    [Fact]
    public async Task AnularPosteaALaRutaDeAnulacion()
    {
        _manejador.Responder(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        await _cliente.AnularFuturaAsync(id);

        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}precios-alimentos/{id}/anular", peticion.Uri.ToString());
    }

    [Fact]
    public async Task ImportarPdfEnviaMultipartConLaParteArchivo()
    {
        var idEsperado = Guid.NewGuid();
        _manejador.Responder(HttpStatusCode.Created, $$"""{"id":"{{idEsperado}}"}""");
        var contenido = new MemoryStream("%PDF-1.7 contenido de prueba"u8.ToArray());

        var id = await _cliente.ImportarPdfAsync(contenido, "notificacion.pdf");

        Assert.Equal(idEsperado, id);
        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal($"{BaseApi}precios-alimentos/importar", peticion.Uri.ToString());
        Assert.StartsWith("multipart/form-data", peticion.TipoDeContenido);
        Assert.Contains("name=archivo", peticion.Cuerpo);
        Assert.Contains("filename=notificacion.pdf", peticion.Cuerpo);
        Assert.Contains("%PDF-1.7", peticion.Cuerpo);
    }

    [Fact]
    public async Task DescargarDocumentoOriginalDevuelveElContenidoPdf()
    {
        var bytes = "%PDF-1.7 documento original de prueba"u8.ToArray();
        _manejador.Responder(HttpStatusCode.OK,
            contenido: bytes, tipoDeContenido: "application/pdf");

        using var flujo = await _cliente.DescargarDocumentoOriginalAsync(Guid.NewGuid());

        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria);
        Assert.Equal(bytes, memoria.ToArray());
        Assert.Equal("Bearer token-actual", _manejador.Peticiones[0].Autorizacion);
    }

    [Fact]
    public async Task Error401RenuevaLaSesionYReintentaLaPeticion()
    {
        _manejador.Responder(HttpStatusCode.Unauthorized);
        _manejador.Responder(HttpStatusCode.OK,
            """{"accessToken":"access-nuevo","expiraEnSegundos":900}""",
            [new("Set-Cookie", "icarus_refresh=refresh-nuevo; Path=/; HttpOnly")]);
        _manejador.Responder(HttpStatusCode.OK, "[]");

        var lista = await _cliente.ListarNotificacionesAsync();

        Assert.Empty(lista);
        await _sesion.Received(1).RenovarTokensAsync(
            "access-nuevo", "refresh-nuevo", Arg.Any<CancellationToken>());
        Assert.Equal(3, _manejador.Peticiones.Count);
        Assert.Equal("icarus_refresh=refresh-actual", _manejador.Peticiones[1].Cookie);
        Assert.Equal("Bearer access-nuevo", _manejador.Peticiones[2].Autorizacion);
    }

    [Fact]
    public async Task RenovacionFallidaPropagaErrorApiException401()
    {
        _manejador.Responder(HttpStatusCode.Unauthorized);
        _manejador.Responder(HttpStatusCode.Unauthorized);

        var error = await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.ListarNotificacionesAsync());

        Assert.Equal(401, error.Estado);
        Assert.Equal(2, _manejador.Peticiones.Count);
    }

    [Fact]
    public async Task SinRefreshTokenUn401SePropagaSinRenovar()
    {
        _sesion.RefreshToken.Returns((string?)null);
        _manejador.Responder(HttpStatusCode.Unauthorized);

        var error = await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.ListarNotificacionesAsync());

        Assert.Equal(401, error.Estado);
        Assert.Single(_manejador.Peticiones);
    }
}
