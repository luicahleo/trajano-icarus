using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using SkiaSharp;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9C Task 5 (spec SP9): la bandeja global de CAISY (GestorRecepcionHuevos)
// lista y filtra los despachos de huevo, confirma la recepción (cierra el
// estado y notifica al tenant) y descarga el recibo PDF solo después de la
// recepción; el tenant consulta su crédito disponible. El sondeo de
// notificaciones con ETag funciona igual en los dos grupos. Los reintentos de
// la confirmación responden 409 sin duplicar nada. Las pruebas comparten la
// base de la colección: la publicación de precios de huevo es global y cada
// prueba publica con una vigencia propia (base distinta a las de las otras
// clases para no colisionar).
[Collection(IntegracionCollection.Nombre)]
public class DespachosHuevoCaisyEndpointsTests
{
    private readonly IdentityFactory _factory;

    public DespachosHuevoCaisyEndpointsTests(IdentityFactory factory) => _factory = factory;

    // Base distinta a PreciosHuevoEndpointsTests/DespachosHuevoEndpointsTests
    // (2025-06-09 + n·30) y a PedidosAlimentoEndpointsTests (2025-07-15 + n·30):
    // dos publicaciones activas no pueden compartir vigencia y las clases
    // comparten la base.
    private static readonly DateOnly FechaNotificacionComun = new(2025, 5, 1);
    private static int _secuencia;

    private static DateOnly VigenciaUnica() =>
        new DateOnly(2025, 8, 5).AddDays(Interlocked.Increment(ref _secuencia) * 30);

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

    private static JsonContent LineasJson() =>
        JsonContent.Create(new
        {
            lineas = new[]
            {
                new { tamano = "Extra", cantidadAmarras = 2, unidadesSueltas = 0 },
                new { tamano = "Primera", cantidadAmarras = 1, unidadesSueltas = 90 },
            },
        });

    private static async Task<Guid> CrearBorradorAsync(HttpClient cliente, string token)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/despachos-huevo", token, LineasJson()));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(cuerpo.GetProperty("id").GetString()!);
    }

    private static async Task<JsonElement> ObtenerDetalleCaisyAsync(HttpClient cliente, string token, Guid id)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo-caisy/{id}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static MultipartFormDataContent DespachoMultipart(byte[] imagen)
    {
        var contenido = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(imagen);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(bytes, "archivo", "nota-despacho.png");
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

    // Alta embebida de una cuenta CAISY con las funcionalidades indicadas.
    private async Task<string> CrearCuentaCaisyAsync(params string[] funcionalidades)
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"despachos-huevo-caisy-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades,
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
    }

    // Importa el XLSX de muestra y lo publica con una vigencia propia en el
    // pasado: queda como publicación vigente para los despachos de la prueba.
    private static async Task ImportarYPublicarHuevoAsync(HttpClient cliente, string token)
    {
        var xlsx = LibroConFechas(FechaNotificacionComun, VigenciaUnica());
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
    }

    private static async Task DespacharAsync(HttpClient cliente, string token, Guid id)
    {
        var envio = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo/{id}/despachar", token,
            DespachoMultipart(ImagenPng())));
        Assert.Equal(HttpStatusCode.NoContent, envio.StatusCode);
    }

    private static async Task<decimal> ObtenerSaldoCreditoAsync(HttpClient cliente, string token)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("saldoDisponible").GetDecimal();
    }

    [Fact]
    public async Task ConsultasSinAutorizacionDevuelven401()
    {
        var cliente = _factory.CreateClient();

        var credito = await cliente.GetAsync("/api/despachos-huevo/credito");
        var bandeja = await cliente.GetAsync("/api/despachos-huevo-caisy");

        Assert.Equal(HttpStatusCode.Unauthorized, credito.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bandeja.StatusCode);
    }

    [Fact]
    public async Task CaisySinGestorRecepcionHuevosNoAccedeALaBandeja()
    {
        var cliente = _factory.CreateClient();
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorPedidoAlimento");
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);

        var bandeja = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo-caisy", tokenCaisy));
        var detalle = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo-caisy/{Guid.NewGuid()}", tokenCaisy));
        var notificaciones = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo-caisy/notificaciones", tokenCaisy));
        var creditoAjeno = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, bandeja.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detalle.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notificaciones.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, creditoAjeno.StatusCode);

        // Con la función correcta la bandeja responde y el crédito del tenant
        // con su función también.
        var tokenRecepcion = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        var bandejaOk = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo-caisy", tokenRecepcion));
        var creditoOk = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/credito", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, bandejaOk.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creditoOk.StatusCode);
    }

    [Fact]
    public async Task LaBandejaCaisyListaFiltraPorEstadoYPagina()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        await ImportarYPublicarHuevoAsync(cliente, tokenCaisy);

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
            ids.Add(await CrearBorradorAsync(cliente, tokenCliente));
        await DespacharAsync(cliente, tokenCliente, ids[0]);

        var primera = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Borrador&pagina=1&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var cuerpoPrimera = await primera.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpoPrimera.GetProperty("total").GetInt32() >= 2);
        var itemsPrimera = cuerpoPrimera.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, itemsPrimera.Count);
        Assert.All(itemsPrimera, d => Assert.Equal("Borrador", d.GetProperty("estado").GetString()));

        var segunda = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Borrador&pagina=2&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var cuerpoSegunda = await segunda.Content.ReadFromJsonAsync<JsonElement>();
        var vistos = itemsPrimera.Concat(cuerpoSegunda.GetProperty("items").EnumerateArray())
            .Select(d => Guid.Parse(d.GetProperty("id").GetString()!))
            .ToList();
        Assert.Contains(ids[1], vistos);
        Assert.Contains(ids[2], vistos);

        // El filtro por estado excluye el despacho ya enviado.
        var despachados = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Despachado", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, despachados.StatusCode);
        var cuerpoDespachados = await despachados.Content.ReadFromJsonAsync<JsonElement>();
        var idsDespachados = cuerpoDespachados.GetProperty("items").EnumerateArray()
            .Select(d => Guid.Parse(d.GetProperty("id").GetString()!));
        Assert.Contains(ids[0], idsDespachados);
        Assert.DoesNotContain(ids[1], idsDespachados);

        // Estado inexistente: 400 con mensaje genérico.
        var estadoInvalido = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/despachos-huevo-caisy?estado=Inexistente", tokenCaisy));
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);

        // Detalle global: CAISY ve el despacho congelado del tenant.
        var detalle = await ObtenerDetalleCaisyAsync(cliente, tokenCaisy, ids[0]);
        Assert.Equal("Despachado", detalle.GetProperty("estado").GetString());
        Assert.True(detalle.GetProperty("totalBs").GetDecimal() > 0);
    }

    [Fact]
    public async Task ConfirmarRecepcionCierraElDespachoNotificaYHabilitaElRecibo()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");
        await ImportarYPublicarHuevoAsync(cliente, tokenCaisy);

        var id = await CrearBorradorAsync(cliente, tokenCliente);
        await DespacharAsync(cliente, tokenCliente, id);
        var saldoAntes = await ObtenerSaldoCreditoAsync(cliente, tokenCliente);

        // El recibo solo existe una vez confirmada la recepción.
        var reciboAntes = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo-caisy/{id}/recibo.pdf", tokenCaisy));
        Assert.Equal(HttpStatusCode.NotFound, reciboAntes.StatusCode);

        var confirmacion = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo-caisy/{id}/confirmar-recepcion", tokenCaisy));
        Assert.Equal(HttpStatusCode.NoContent, confirmacion.StatusCode);

        var detalle = await ObtenerDetalleCaisyAsync(cliente, tokenCaisy, id);
        Assert.Equal("Recibido", detalle.GetProperty("estado").GetString());

        // El crédito por la recepción se libera recién catorce días después
        // (spec SP9): una recepción confirmada hoy todavía no mueve el saldo.
        Assert.Equal(saldoAntes, await ObtenerSaldoCreditoAsync(cliente, tokenCliente));

        // El tenant recibe la notificación de la recepción confirmada.
        var bandeja = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/notificaciones", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, bandeja.StatusCode);
        var notificaciones = await bandeja.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(notificaciones.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("tipo").GetString() == "DespachoRecibido"
                && n.GetProperty("despachoHuevoId").GetGuid() == id);

        // Un segundo intento choca con el estado: 409 sin duplicar nada.
        var reintento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/despachos-huevo-caisy/{id}/confirmar-recepcion", tokenCaisy));
        Assert.Equal(HttpStatusCode.Conflict, reintento.StatusCode);

        // Recibo PDF real después de la recepción.
        var recibo = await cliente.SendAsync(
            Pedido(HttpMethod.Get, $"/api/despachos-huevo-caisy/{id}/recibo.pdf", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, recibo.StatusCode);
        Assert.StartsWith("application/pdf", recibo.Content.Headers.ContentType!.ToString());
        var bytes = await recibo.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
    }

    [Fact]
    public async Task ElSondeoDeNotificacionesRespetaElEtagEnAmbosGrupos()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenCaisy = await CrearCuentaCaisyAsync("GestorRecepcionHuevos");

        // Grupo del tenant: 304 con el mismo ETag y cambio de huella al leer.
        var sondeoTenant = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo/notificaciones", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, sondeoTenant.StatusCode);
        var etagTenant = sondeoTenant.Headers.ETag!.ToString();

        var sondeoIguTenant = new HttpRequestMessage(
            HttpMethod.Get, "/api/despachos-huevo/notificaciones");
        sondeoIguTenant.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenCliente);
        sondeoIguTenant.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etagTenant));
        var respuestaIguTenant = await cliente.SendAsync(sondeoIguTenant);
        Assert.Equal(HttpStatusCode.NotModified, respuestaIguTenant.StatusCode);

        // Un id inexistente responde 404 genérico (la notificación ajena o
        // inexistente no se distingue).
        var marcarAjena = await cliente.SendAsync(Pedido(
            HttpMethod.Post,
            $"/api/despachos-huevo/notificaciones/{Guid.NewGuid()}/marcar-leida",
            tokenCliente));
        Assert.Equal(HttpStatusCode.NotFound, marcarAjena.StatusCode);

        var bandejaTenant = await sondeoTenant.Content.ReadFromJsonAsync<JsonElement>();
        var itemsTenant = bandejaTenant.GetProperty("items").EnumerateArray().ToList();
        if (itemsTenant.Count > 0)
        {
            // La bandeja del tenant se comparte con otras clases de la
            // colección: solo si hay ítems se valida el efecto del marcado.
            var marcado = await cliente.SendAsync(Pedido(
                HttpMethod.Post,
                $"/api/despachos-huevo/notificaciones/{itemsTenant[0].GetProperty("id").GetGuid()}/marcar-leida",
                tokenCliente));
            Assert.Equal(HttpStatusCode.NoContent, marcado.StatusCode);

            var sondeoTrasLeer = await cliente.SendAsync(
                Pedido(HttpMethod.Get, "/api/despachos-huevo/notificaciones", tokenCliente));
            var bandejaTrasLeer = await sondeoTrasLeer.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(bandejaTenant.GetProperty("contador").GetInt32() - 1,
                bandejaTrasLeer.GetProperty("contador").GetInt32());
            Assert.NotEqual(etagTenant, sondeoTrasLeer.Headers.ETag!.ToString());
        }

        // Grupo de CAISY: la bandeja global también sondea con 304.
        var sondeoCaisy = await cliente.SendAsync(
            Pedido(HttpMethod.Get, "/api/despachos-huevo-caisy/notificaciones", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, sondeoCaisy.StatusCode);
        var etagCaisy = sondeoCaisy.Headers.ETag!.ToString();

        var sondeoIgualCaisy = new HttpRequestMessage(
            HttpMethod.Get, "/api/despachos-huevo-caisy/notificaciones");
        sondeoIgualCaisy.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenCaisy);
        sondeoIgualCaisy.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etagCaisy));
        var respuestaIgualCaisy = await cliente.SendAsync(sondeoIgualCaisy);
        Assert.Equal(HttpStatusCode.NotModified, respuestaIgualCaisy.StatusCode);
    }
}
