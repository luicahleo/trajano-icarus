using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// SP8A Tarea 4 (spec: "Notificación de Precios de Alimentos"): los precios son
// un catálogo global que solo consulta y publica una cuenta CAISY con la
// función GestorPedidoAlimento. El PDF original queda privado y descargable,
// la publicación es una confirmación explícita y nunca se registra contenido
// del documento en los logs.
[Collection(IntegracionCollection.Nombre)]
public class PreciosAlimentosEndpointsTests
{
    private readonly IdentityFactory _factory;

    public PreciosAlimentosEndpointsTests(IdentityFactory factory) => _factory = factory;

    // La colección comparte la base de datos y dos publicaciones activas no
    // pueden compartir vigencia (regla del spec): cada prueba usa su propia
    // fecha de vigencia, posterior a las fechas de documento (ningún control
    // de «Precio actual» ajeno interfiere).
    private static readonly DateOnly FechaDocumentoComun = new(2025, 4, 1);
    private static readonly DateOnly FechaDocumentoControl = new(2025, 6, 1);
    private static readonly DateOnly VigenciaBaseline = new(2025, 5, 1);
    private static readonly DateOnly VigenciaFutura = new(2026, 12, 1);
    private static int _secuencia;

    private static string ContrasenaSemilla => IdentityFactory.ContrasenaDePrueba;

    private static DateOnly VigenciaUnica() =>
        new DateOnly(2025, 6, 9).AddDays(Interlocked.Increment(ref _secuencia) * 30);

    private static string F(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static FileStream AbrirFixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NotificacionPreciosMuestra.pdf"));

    // Reemplazos de igual longitud: conservan el largo declarado del flujo PDF.
    private static byte[] FixtureConFechas(DateOnly fechaDocumento, DateOnly vigenteDesde)
    {
        using var fixture = AbrirFixture();
        var bytes = new byte[fixture.Length];
        fixture.ReadExactly(bytes);
        var texto = System.Text.Encoding.ASCII.GetString(bytes);
        texto = texto
            .Replace("02/11/2025", F(fechaDocumento))
            .Replace("10/11/2025", F(vigenteDesde));
        return System.Text.Encoding.ASCII.GetBytes(texto);
    }

    private async Task<(HttpClient Cliente, string Token)> CrearCuentaCaisyConFuncion()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"precios-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var token = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
        var cliente = _factory.CreateClient();
        return (cliente, token);
    }

    private static async Task<string> LoginComo(HttpClient cliente, string email, string? contrasena = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = contrasena ?? ContrasenaSemilla });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Pedido(
        HttpMethod metodo, string url, string token, HttpContent? contenido = null) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = contenido,
        };

    private static MultipartFormDataContent MultipartPdf(byte[] pdf)
    {
        var contenido = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(pdf);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        contenido.Add(bytes, "archivo", "notificacion.pdf");
        return contenido;
    }

    private static async Task<Guid> ImportarConAsync(HttpClient cliente, string token, byte[] pdf)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/precios-alimentos/importar", token, MultipartPdf(pdf)));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(cuerpo.GetProperty("id").GetString()!);
    }

    private static async Task<HttpStatusCode> PublicarAsync(HttpClient cliente, string token, Guid id)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-alimentos/{id}/publicar", token));
        return respuesta.StatusCode;
    }

    private static async Task<JsonElement> ObtenerAsync(HttpClient cliente, string token, string url)
    {
        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Get, url, token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task ClienteYTrabajadorNoAccedenAlCatalogoDePrecios()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);

        var respuestaCliente = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-alimentos", tokenCliente));
        var respuestaTrabajador = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-alimentos", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuestaCliente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, respuestaTrabajador.StatusCode);
    }

    [Fact]
    public async Task CuentaCaisySinLaFuncionNoAccedeAlCatalogo()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"sin-funcion-{Guid.NewGuid():N}@icarus.test";
        await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = Array.Empty<string>(),
            })));
        var tokenCaisy = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");

        var respuesta = await anonimo.SendAsync(Pedido(HttpMethod.Get, "/api/precios-alimentos", tokenCaisy));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task FlujoCompletoImportarRevisarPublicarYConsultarVigente()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();

        var id = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, vigencia));

        // El borrador importado está completo y editable.
        var cuerpo = await ObtenerAsync(cliente, token, $"/api/precios-alimentos/{id}");
        Assert.Equal("Borrador", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(12, cuerpo.GetProperty("detalles").GetArrayLength());
        Assert.Equal(1.20m, cuerpo.GetProperty("aporteCaisy").GetDecimal());
        Assert.Equal(0.60m, cuerpo.GetProperty("fondo").GetDecimal());
        Assert.Equal(0.75m, cuerpo.GetProperty("servicios").GetDecimal());
        Assert.Equal(JsonValueKind.String, cuerpo.GetProperty("documentoOriginalId").ValueKind);

        // Revisión del borrador: el Gestor corrige un precio antes de publicar.
        var detalles = cuerpo.GetProperty("detalles").EnumerateArray()
            .Select(d => new
            {
                tipoAlimento = d.GetProperty("tipoAlimento").GetString()!,
                presentacion = d.GetProperty("presentacion").GetString()!,
                precioFinalPor40Kg = d.GetProperty("precioFinalPor40Kg").GetDecimal(),
                precioActualDocumento = d.GetProperty("precioActualDocumento").ValueKind == JsonValueKind.Null
                    ? (decimal?)null : d.GetProperty("precioActualDocumento").GetDecimal(),
                edadDesdeDias = d.GetProperty("edadDesdeDias").ValueKind == JsonValueKind.Null
                    ? (int?)null : d.GetProperty("edadDesdeDias").GetInt32(),
                edadHastaDias = d.GetProperty("edadHastaDias").ValueKind == JsonValueKind.Null
                    ? (int?)null : d.GetProperty("edadHastaDias").GetInt32(),
            })
            .ToList();
        var detallesRevisados = detalles
            .Select(d => d.tipoAlimento == "Iniciador" && d.presentacion == "Bolsa"
                ? new
                {
                    d.tipoAlimento,
                    d.presentacion,
                    precioFinalPor40Kg = 177.00m,
                    d.precioActualDocumento,
                    d.edadDesdeDias,
                    d.edadHastaDias,
                }
                : d)
            .ToList();
        var revision = await cliente.SendAsync(Pedido(HttpMethod.Put, $"/api/precios-alimentos/{id}", token,
            JsonContent.Create(new
            {
                fechaDocumento = FechaDocumentoComun.ToString("yyyy-MM-dd"),
                vigenteDesde = vigencia.ToString("yyyy-MM-dd"),
                aporteCaisy = 1.20m,
                fondo = 0.60m,
                servicios = 0.75m,
                detalles = detallesRevisados,
            })));
        Assert.Equal(HttpStatusCode.NoContent, revision.StatusCode);

        // Publicación: confirmación explícita.
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, id));

        // Vigente en la fecha exacta de la vigencia; antes de ella no hay nada.
        var vigente = await ObtenerAsync(
            cliente, token, $"/api/precios-alimentos/vigente?fecha={vigencia.ToString("yyyy-MM-dd")}");
        Assert.Equal(id, Guid.Parse(vigente.GetProperty("id").GetString()!));
        var detalleRevisado = vigente.GetProperty("detalles").EnumerateArray()
            .Single(d => d.GetProperty("tipoAlimento").GetString() == "Iniciador"
                && d.GetProperty("presentacion").GetString() == "Bolsa");
        Assert.Equal(177.00m, detalleRevisado.GetProperty("precioFinalPor40Kg").GetDecimal());

        var sinVigente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/precios-alimentos/vigente?fecha=2024-01-01", token));
        Assert.Equal(HttpStatusCode.NotFound, sinVigente.StatusCode);

        var hoy = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-alimentos/vigente", token));
        Assert.Equal(HttpStatusCode.OK, hoy.StatusCode);

        var historial = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-alimentos", token));
        Assert.Equal(HttpStatusCode.OK, historial.StatusCode);
        var items = await historial.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("estado").GetString() == "Publicada");

        // El original privado se descarga como adjunto autorizado.
        var original = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/precios-alimentos/{id}/documento-original", token));
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var bytes = await original.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public async Task ImportarRechazaUnArchivoSinFirmaPdf()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();

        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/precios-alimentos/importar", token,
            MultipartPdf("esto no es un pdf"u8.ToArray())));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task PublicarDosVecesConLaMismaVigenciaDevuelve409()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();
        var primero = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, vigencia));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, primero));

        // Segunda publicación con la misma vigencia: conflicto genérico.
        var segundo = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, vigencia));
        var duplicado = await PublicarAsync(cliente, token, segundo);

        Assert.Equal(HttpStatusCode.Conflict, duplicado);
    }

    [Fact]
    public async Task ElPrecioActualDiscrepanteBloqueaLaPublicacion()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();

        // Publicación base: rige desde antes de la fecha del segundo documento,
        // de modo que la columna «Precio actual» del segundo tenga contra qué
        // controlarse. (176.50 es el precio nuevo del documento base.)
        var baseVigente = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoControl, VigenciaBaseline));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, baseVigente));

        var borrador = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoControl, VigenciaUnica()));
        var respuesta = await PublicarAsync(cliente, token, borrador);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta);
        var detalle = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/precios-alimentos/{borrador}", token));
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        // La extracción del borrador no se ve afectada: sigue editable.
        var cuerpo = await detalle.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Borrador", cuerpo.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task AnularUnaPublicacionFuturaNoAlteraLaVigente()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();
        var vigente = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, vigencia));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, vigente));

        // Vigencia futura: se publica anticipadamente y se anula antes de
        // entrar en vigor.
        var futura = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, VigenciaFutura));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, futura));

        var anular = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-alimentos/{futura}/anular", token));
        Assert.Equal(HttpStatusCode.NoContent, anular.StatusCode);

        var detalleAnulada = await ObtenerAsync(cliente, token, $"/api/precios-alimentos/{futura}");
        Assert.Equal("Anulada", detalleAnulada.GetProperty("estado").GetString());

        var sigueVigente = await ObtenerAsync(
            cliente, token, $"/api/precios-alimentos/vigente?fecha={vigencia.ToString("yyyy-MM-dd")}");
        Assert.Equal(vigente, Guid.Parse(sigueVigente.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task UnaPublicacionEfectivaNoSePuedeAnular()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();
        var vigente = await ImportarConAsync(
            cliente, token, FixtureConFechas(FechaDocumentoComun, vigencia));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, vigente));

        var anular = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-alimentos/{vigente}/anular", token));

        Assert.Equal(HttpStatusCode.BadRequest, anular.StatusCode);
    }

    [Fact]
    public async Task ImportarUnArchivoExcesivoDevuelve413()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var excesivo = new byte[21 * 1024 * 1024];
        "%PDF-"u8.CopyTo(excesivo);

        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/precios-alimentos/importar", token,
            MultipartPdf(excesivo)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
    }

    [Fact]
    public async Task ImportarSinArchivoDevuelve400()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();

        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/precios-alimentos/importar", token,
            new MultipartFormDataContent()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConsultasSinAutorizacionDevuelven401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/precios-alimentos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
