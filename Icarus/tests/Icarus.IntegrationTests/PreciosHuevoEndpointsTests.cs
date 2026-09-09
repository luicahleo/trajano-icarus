using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9A Task 5 (spec: "Publicaciones de Precio de Huevo CAISY"): el catálogo
// global de precios de huevo solo lo consulta y publica una cuenta CAISY con
// la función GestorRecepcionHuevos. El XLSX original queda privado y
// descargable, la publicación es una confirmación explícita y nunca se
// registra contenido del documento en los logs (anti-PII).
[Collection(IntegracionCollection.Nombre)]
public class PreciosHuevoEndpointsTests
{
    private readonly IdentityFactory _factory;

    public PreciosHuevoEndpointsTests(IdentityFactory factory) => _factory = factory;

    // La colección comparte la base de datos y dos publicaciones activas no
    // pueden compartir vigencia (regla del spec): cada prueba usa su propia
    // fecha de vigencia, posterior a la fecha de notificación común.
    private static readonly DateOnly FechaNotificacionComun = new(2025, 4, 1);
    private static int _secuencia;

    private static string ContrasenaSemilla => IdentityFactory.ContrasenaDePrueba;

    private static DateOnly VigenciaUnica() =>
        new DateOnly(2025, 6, 9).AddDays(Interlocked.Increment(ref _secuencia) * 30);

    // Genera en memoria el formato real de CAISY para el cambio de precio de
    // huevo: fechas de notificación y vigencia, y la tabla TAMANO / PRECIO
    // ACTUAL / NUEVO PRECIO AL PRODUCTOR / SERVICIOS con un servicio común.
    private static byte[] LibroConFechas(DateOnly fechaNotificacion, DateOnly fechaVigencia)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Precios");
        hoja.Cell(1, 1).Value = "FECHA DE NOTIFICACION";
        hoja.Cell(1, 2).Value = fechaNotificacion.ToDateTime(TimeOnly.MinValue);
        hoja.Cell(2, 1).Value = "FECHA DE ENTRADA EN VIGENCIA";
        hoja.Cell(2, 2).Value = fechaVigencia.ToDateTime(TimeOnly.MinValue);
        hoja.Cell(4, 1).Value = "TAMANO";
        hoja.Cell(4, 2).Value = "PRECIO ACTUAL AL PRODUCTOR";
        hoja.Cell(4, 3).Value = "NUEVO PRECIO AL PRODUCTOR";
        hoja.Cell(4, 4).Value = "SERVICIOS";
        var filas = new (string Tamano, decimal Precio)[]
        {
            ("EXTRA", 1.50m), ("PRIMERA", 1.40m), ("SEGUNDA", 1.30m),
            ("TERCERA", 1.20m), ("CUARTA", 1.10m), ("QUINTA", 1.00m),
        };
        for (var i = 0; i < filas.Length; i++)
        {
            hoja.Cell(5 + i, 1).Value = filas[i].Tamano;
            hoja.Cell(5 + i, 3).Value = filas[i].Precio;
            hoja.Cell(5 + i, 4).Value = 0.05m;
        }
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private async Task<(HttpClient Cliente, string Token)> CrearCuentaCaisyConFuncion()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"precios-huevo-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorRecepcionHuevos" },
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

    private static MultipartFormDataContent MultipartXlsx(byte[] xlsx)
    {
        var contenido = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(xlsx);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(bytes, "archivo", "precios-huevo.xlsx");
        return contenido;
    }

    private static async Task<Guid> ImportarConAsync(HttpClient cliente, string token, byte[] xlsx)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/precios-huevo-caisy/importar", token, MultipartXlsx(xlsx)));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(cuerpo.GetProperty("id").GetString()!);
    }

    private static async Task<HttpStatusCode> PublicarAsync(HttpClient cliente, string token, Guid id)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-huevo-caisy/{id}/publicar", token));
        return respuesta.StatusCode;
    }

    private static async Task<JsonElement> ObtenerAsync(HttpClient cliente, string token, string url)
    {
        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Get, url, token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task FlujoCompletoImportarRevisarPublicarYConsultarVigente()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();

        var id = await ImportarConAsync(cliente, token, LibroConFechas(FechaNotificacionComun, vigencia));

        // El borrador importado está completo y editable.
        var cuerpo = await ObtenerAsync(cliente, token, $"/api/precios-huevo-caisy/{id}");
        Assert.Equal("Borrador", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(6, cuerpo.GetProperty("detalles").GetArrayLength());
        Assert.Equal(0.05m, cuerpo.GetProperty("servicio").GetDecimal());
        Assert.Equal(JsonValueKind.String, cuerpo.GetProperty("documentoOriginalId").ValueKind);

        // Revisión del borrador: el Gestor corrige el precio de Extra antes de
        // publicar; el resto de la tabla se conserva como la extrajo el
        // importador.
        var detalles = cuerpo.GetProperty("detalles").EnumerateArray()
            .Select(d => new
            {
                tamano = d.GetProperty("tamano").GetString()!,
                precioAlProductor = d.GetProperty("precioAlProductor").GetDecimal(),
                precioActualDocumento = d.GetProperty("precioActualDocumento").ValueKind == JsonValueKind.Null
                    ? (decimal?)null : d.GetProperty("precioActualDocumento").GetDecimal(),
            })
            .ToList();
        var detallesRevisados = detalles
            .Select(d => d.tamano == "Extra"
                ? new { d.tamano, precioAlProductor = 1.55m, d.precioActualDocumento }
                : d)
            .ToList();
        var revision = await cliente.SendAsync(Pedido(HttpMethod.Put, $"/api/precios-huevo-caisy/{id}", token,
            JsonContent.Create(new
            {
                fechaNotificacion = FechaNotificacionComun.ToString("yyyy-MM-dd"),
                fechaVigencia = vigencia.ToString("yyyy-MM-dd"),
                servicio = 0.05m,
                detalles = detallesRevisados,
            })));
        Assert.Equal(HttpStatusCode.NoContent, revision.StatusCode);

        // Publicación: confirmación explícita.
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, id));

        // Vigente en la fecha exacta de la vigencia; antes de ella no hay nada.
        var vigente = await ObtenerAsync(
            cliente, token, $"/api/precios-huevo-caisy/vigente?fecha={vigencia.ToString("yyyy-MM-dd")}");
        Assert.Equal(id, Guid.Parse(vigente.GetProperty("id").GetString()!));
        var detalleRevisado = vigente.GetProperty("detalles").EnumerateArray()
            .Single(d => d.GetProperty("tamano").GetString() == "Extra");
        Assert.Equal(1.55m, detalleRevisado.GetProperty("precioAlProductor").GetDecimal());
        Assert.Equal(1.60m, detalleRevisado.GetProperty("precioUnitario").GetDecimal());

        var sinVigente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/precios-huevo-caisy/vigente?fecha=2024-01-01", token));
        Assert.Equal(HttpStatusCode.NotFound, sinVigente.StatusCode);

        var hoy = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-huevo-caisy/vigente", token));
        Assert.Equal(HttpStatusCode.OK, hoy.StatusCode);

        var historial = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-huevo-caisy", token));
        Assert.Equal(HttpStatusCode.OK, historial.StatusCode);
        var items = await historial.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("estado").GetString() == "Publicada");

        // El original privado se descarga como adjunto autorizado (libro XLSX).
        var original = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/precios-huevo-caisy/{id}/documento-original", token));
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var bytes = await original.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    [Fact]
    public async Task ImportarSinArchivoDevuelve400()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();

        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/precios-huevo-caisy/importar", token,
            new MultipartFormDataContent()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ImportarUnPdfDevuelve400()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var contenido = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("%PDF-falso"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        contenido.Add(bytes, "archivo", "precios-huevo.pdf");

        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Post, "/api/precios-huevo-caisy/importar", token, contenido));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ImportarUnArchivoExcesivoDevuelve413()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var excesivo = new byte[5 * 1024 * 1024 + 1024];
        Array.Copy("PK"u8.ToArray(), excesivo, 2);

        var respuesta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/precios-huevo-caisy/importar", token,
            MultipartXlsx(excesivo)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
    }

    [Fact]
    public async Task PublicarDosVecesConLaMismaVigenciaDevuelve409()
    {
        var (cliente, token) = await CrearCuentaCaisyConFuncion();
        var vigencia = VigenciaUnica();
        var primero = await ImportarConAsync(cliente, token, LibroConFechas(FechaNotificacionComun, vigencia));
        Assert.Equal(HttpStatusCode.NoContent, await PublicarAsync(cliente, token, primero));

        // Segunda publicación con la misma vigencia: conflicto genérico.
        var segundo = await ImportarConAsync(cliente, token, LibroConFechas(FechaNotificacionComun, vigencia));
        var duplicado = await PublicarAsync(cliente, token, segundo);

        Assert.Equal(HttpStatusCode.Conflict, duplicado);
    }

    [Fact]
    public async Task ClienteYTrabajadorNoAccedenAlCatalogoDePreciosDeHuevo()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);

        var respuestaCliente = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-huevo-caisy", tokenCliente));
        var respuestaTrabajador = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/precios-huevo-caisy", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuestaCliente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, respuestaTrabajador.StatusCode);
    }

    [Fact]
    public async Task CuentaCaisyConSoloGestorPedidoAlimentoDevuelve403()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"solo-pedido-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var tokenCaisy = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");

        var respuesta = await anonimo.SendAsync(Pedido(HttpMethod.Get, "/api/precios-huevo-caisy", tokenCaisy));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConsultasSinAutorizacionDevuelven401()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/precios-huevo-caisy");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
