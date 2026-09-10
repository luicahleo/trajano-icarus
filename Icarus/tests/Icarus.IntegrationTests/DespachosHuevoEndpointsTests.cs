using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using SkiaSharp;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9B Task 4 (spec SP9): el tenant con la función DespachoHuevo gestiona sus
// borradores de despacho de huevo y despacha con la foto de la nota; el envío
// congela el precio al productor vigente del catálogo global de CAISY (SP9A).
// Un reintento responde 409 sin repetir la transición. Las pruebas comparten
// la base de la colección: cada prueba usa su propio tenant o borradores sin
// envío para no depender del orden. La publicación de precios de huevo es
// global: los precios congelados se contrastan contra la publicación vigente
// leída por API, sin asumir cifras, porque otra clase de la colección puede
// haber publicado con una vigencia posterior.
[Collection(IntegracionCollection.Nombre)]
public class DespachosHuevoEndpointsTests
{
    private readonly IdentityFactory _factory;

    public DespachosHuevoEndpointsTests(IdentityFactory factory) => _factory = factory;

    // Base distinta a la de PreciosHuevoEndpointsTests (2025-06-09 + n·30):
    // dos publicaciones activas no pueden compartir vigencia y las clases
    // comparten la base. El desfase de 15 días evita la colisión.
    private static readonly DateOnly FechaNotificacionComun = new(2025, 4, 1);
    private static int _secuencia;

    private static DateOnly VigenciaUnica() =>
        new DateOnly(2025, 6, 24).AddDays(Interlocked.Increment(ref _secuencia) * 30);

    private static async Task<string> LoginComo(HttpClient cliente, string email, string? contrasena = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = contrasena ?? IdentityFactory.ContrasenaDePrueba });
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

    private static JsonContent LineasJson(int amarrasExtra = 2, int sueltasExtra = 0,
        int amarrasPrimera = 1, int sueltasPrimera = 90) =>
        JsonContent.Create(new
        {
            lineas = new[]
            {
                new { tamano = "Extra", cantidadAmarras = amarrasExtra, unidadesSueltas = sueltasExtra },
                new { tamano = "Primera", cantidadAmarras = amarrasPrimera, unidadesSueltas = sueltasPrimera },
            },
        });

    private static async Task<Guid> CrearBorradorAsync(HttpClient cliente, string token) =>
        await CrearBorradorConAsync(cliente, token, LineasJson());

    private static async Task<Guid> CrearBorradorConAsync(HttpClient cliente, string token, JsonContent lineas)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/despachos-huevo", token, lineas));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(cuerpo.GetProperty("id").GetString()!);
    }

    private static async Task<JsonElement> ObtenerDetalleAsync(HttpClient cliente, string token, Guid id)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/despachos-huevo/{id}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static MultipartFormDataContent DespachoMultipart(byte[]? imagen)
    {
        var contenido = new MultipartFormDataContent();
        if (imagen is not null)
        {
            var bytes = new ByteArrayContent(imagen);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            contenido.Add(bytes, "archivo", "nota-despacho.png");
        }
        return contenido;
    }

    // Imagen PNG mínima válida: el almacén de respaldos exige firma real,
    // decodificación SkiaSharp y ausencia de datos sobrantes.
    private static byte[] ImagenPng(int ancho = 8, int alto = 6)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        bitmap.Erase(SKColors.Gray);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 90);
        return datos.ToArray();
    }

    // Genera en memoria el formato real de CAISY para el cambio de precio de
    // huevo (mismo helper que PreciosHuevoEndpointsTests, SP9A).
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
        var emailCaisy = $"despachos-huevo-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorRecepcionHuevos" },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var token = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
        return (_factory.CreateClient(), token);
    }

    // Importa el XLSX de muestra y lo publica con una vigencia propia en el
    // pasado: queda como publicación vigente para los despachos de la prueba.
    private static async Task<Guid> ImportarYPublicarHuevoAsync(HttpClient cliente, string token)
    {
        var vigencia = VigenciaUnica();
        var xlsx = LibroConFechas(FechaNotificacionComun, vigencia);
        var contenido = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(xlsx);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(bytes, "archivo", "precios-huevo.xlsx");
        var alta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/precios-huevo-caisy/importar", token, contenido));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var idPublicacion = Guid.Parse((await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        var publicacion = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-huevo-caisy/{idPublicacion}/publicar", token));
        Assert.Equal(HttpStatusCode.NoContent, publicacion.StatusCode);
        return idPublicacion;
    }

    // Alta embebida de un cliente avícola (módulo GestionAvicola) sin granja:
    // el borrador de despacho exige una granja activa del tenant.
    private async Task<string> CrearClienteSinGranjaAsync()
    {
        var admin = await LoginComo(_factory.CreateClient(), SemillaIdentidad.EmailAdmin);
        var cliente = _factory.CreateClient();
        var email = $"sin-granja-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", admin,
            JsonContent.Create(new
            {
                razonSocial = "Avícola de Prueba S.A.C.",
                identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
                email,
                contrasena = IdentityFactory.ContrasenaDePrueba,
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{id}/modulos", admin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return await LoginComo(cliente, email);
    }

    [Fact]
    public async Task ConsultasSinAutorizacionDevuelven401()
    {
        var cliente = _factory.CreateClient();

        var lista = await cliente.GetAsync("/api/despachos-huevo");
        var vigentes = await cliente.GetAsync("/api/despachos-huevo/precios-vigentes");

        Assert.Equal(HttpStatusCode.Unauthorized, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, vigentes.StatusCode);
    }

    [Fact]
    public async Task TrabajadorSinLaFuncionNoAccedeYElClienteSi()
    {
        var cliente = _factory.CreateClient();
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);

        var trabajador = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo", tokenTrabajador));
        var clienteOk = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo", tokenCliente));

        Assert.Equal(HttpStatusCode.Forbidden, trabajador.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clienteOk.StatusCode);
    }

    [Fact]
    public async Task CrearEditarListarYDesactivarBorrador()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);

        // Alta inválida: un tamaño de huevo inexistente se rechaza.
        var invalido = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/despachos-huevo", tokenCliente,
            JsonContent.Create(new
            {
                lineas = new[]
                {
                    new { tamano = "Inexistente", cantidadAmarras = 1, unidadesSueltas = 0 },
                },
            })));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var id = await CrearBorradorAsync(cliente, tokenCliente);
        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, id);
        Assert.Equal("Borrador", detalle.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, detalle.GetProperty("fechaDespacho").ValueKind);
        Assert.Equal(JsonValueKind.Null, detalle.GetProperty("totalBs").ValueKind);
        Assert.Equal(2, detalle.GetProperty("detalles").GetArrayLength());
        Assert.Equal(630, detalle.GetProperty("totalHuevos").GetInt32());

        var edicion = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/despachos-huevo/{id}", tokenCliente, LineasJson(amarrasExtra: 3)));
        Assert.Equal(HttpStatusCode.NoContent, edicion.StatusCode);
        var editado = await ObtenerDetalleAsync(cliente, tokenCliente, id);
        var extra = editado.GetProperty("detalles").EnumerateArray()
            .Single(d => d.GetProperty("tamano").GetString() == "Extra");
        Assert.Equal(3, extra.GetProperty("cantidadAmarras").GetInt32());

        var lista = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/despachos-huevo", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        Assert.Contains((await lista.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            d => d.GetProperty("id").GetString() == id.ToString());

        var borrado = await cliente.SendAsync(
            Pedido(HttpMethod.Delete, $"/api/despachos-huevo/{id}", tokenCliente));
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        // El filtro de bajas del DbContext lo vuelve ajeno: 404 genérico.
        var trasBorrar = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo/{id}", tokenCliente));
        Assert.Equal(HttpStatusCode.NotFound, trasBorrar.StatusCode);
    }

    [Fact]
    public async Task SinGranjaActivaNoCreaBorrador()
    {
        var cliente = _factory.CreateClient();
        var tokenSinGranja = await CrearClienteSinGranjaAsync();

        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/despachos-huevo", tokenSinGranja, LineasJson()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task DespacharExigeFotoYRespetaElLimiteDeTamano()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var sinFoto = await CrearBorradorAsync(cliente, tokenCliente);

        // Sin archivo: la foto de la nota es obligatoria en la misma operación.
        var sinArchivo = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{sinFoto}/despachar", tokenCliente,
            DespachoMultipart(null)));
        Assert.Equal(HttpStatusCode.BadRequest, sinArchivo.StatusCode);

        // Archivo vacío: también se rechaza antes de tocar el despacho.
        var vacio = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{sinFoto}/despachar", tokenCliente,
            DespachoMultipart([])));
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);

        // Archivo mayor al límite (5 MiB): 413, sin tocar el estado.
        var excesivo = new byte[5 * 1024 * 1024 + 1024];
        Array.Copy(ImagenPng(), excesivo, 8);
        var grande = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{sinFoto}/despachar", tokenCliente,
            DespachoMultipart(excesivo)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, grande.StatusCode);

        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, sinFoto);
        Assert.Equal("Borrador", detalle.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task DespacharCongelaElPrecioVigenteYUnReintentoDevuelve409()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarHuevoAsync(caisy, tokenCaisy);

        // El precio congelado se contrasta contra la publicación vigente leída
        // por API: el catálogo es global y otra clase puede haber publicado
        // con una vigencia posterior dentro de la base compartida.
        var vigente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo/precios-vigentes", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        var cuerpoVigente = await vigente.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Publicada", cuerpoVigente.GetProperty("estado").GetString());
        var precios = cuerpoVigente.GetProperty("detalles").EnumerateArray()
            .ToDictionary(d => d.GetProperty("tamano").GetString()!,
                d => d.GetProperty("precioUnitario").GetDecimal());

        var id = await CrearBorradorAsync(cliente, tokenCliente);
        var envio = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{id}/despachar", tokenCliente,
            DespachoMultipart(ImagenPng())));
        Assert.Equal(HttpStatusCode.NoContent, envio.StatusCode);

        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, id);
        Assert.Equal("Despachado", detalle.GetProperty("estado").GetString());
        Assert.Equal(630, detalle.GetProperty("totalHuevos").GetInt32());
        var detalles = detalle.GetProperty("detalles").EnumerateArray().ToList();
        var extra = detalles.Single(d => d.GetProperty("tamano").GetString() == "Extra");
        var primera = detalles.Single(d => d.GetProperty("tamano").GetString() == "Primera");
        Assert.Equal(precios["Extra"], extra.GetProperty("precioUnitarioCongelado").GetDecimal());
        Assert.Equal(precios["Primera"], primera.GetProperty("precioUnitarioCongelado").GetDecimal());
        Assert.Equal(360 * precios["Extra"], extra.GetProperty("subtotal").GetDecimal());
        Assert.Equal(270 * precios["Primera"], primera.GetProperty("subtotal").GetDecimal());
        Assert.Equal(360 * precios["Extra"] + 270 * precios["Primera"],
            detalle.GetProperty("totalBs").GetDecimal());

        // Un segundo envío es un reintento sobre lo ya Despachado: 409.
        var reintento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{id}/despachar", tokenCliente,
            DespachoMultipart(ImagenPng())));
        Assert.Equal(HttpStatusCode.Conflict, reintento.StatusCode);

        // El detalle de otro tenant no existe: 404 genérico.
        var tokenC2 = await CrearClienteSinGranjaAsync();
        var ajeno = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo/{id}", tokenC2));
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
    }
}
