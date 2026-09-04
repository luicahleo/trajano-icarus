using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using SkiaSharp;
using Xunit;

namespace Icarus.IntegrationTests;

// SP8C Tarea 2 (spec: "Documentos privados"): los respaldos de la nota son
// privados y probatorios. CAISY los sube sobre un pedido despachado; solo el
// tenant propietario y CAISY los descargan (un tercero recibe 404 o 403 sin
// revelar existencia). La vista derivada se sirve inline y el original solo
// como adjunto con cabeceras seguras. La sustitución desactiva el previo
// conservando el histórico. Cada prueba crea su propio tenant: no comparte
// cupo semanal con las pruebas de SP8B.
[Collection(IntegracionCollection.Nombre)]
public class DocumentosNotaEndpointsTests
{
    private readonly IdentityFactory _factory;

    public DocumentosNotaEndpointsTests(IdentityFactory factory) => _factory = factory;

    // La colección comparte la base: la clase usa UNA sola publicación con
    // vigencia en la ventana libre: posterior a la fecha de documento de
    // control de Precios (2025-06-01, cuya publicación base exige vigencia
    // nula hasta esa fecha) y a las de documento de Pedidos (2025-01-01) y de
    // Precios (2025-04-01); y anterior a la baseline de Precios (2025-05-01
    // queda cubierta por su fecha de control) y a las vigencias únicas de
    // Precios (2025-07-09+) y de Pedidos (2025-08-14+). Las clases corren en
    // orden alfabético dentro de la colección y esta es la primera (D). Los
    // envíos resuelven la publicación vigente en la fecha de negocio: todas
    // publican el mismo fixture, así que cualquiera sirve.
    private static Guid _idPublicacionClase;

    private static async Task<Guid> AsegurarPublicacionDeClaseAsync(
        HttpClient cliente, string tokenCaisy)
    {
        if (_idPublicacionClase != Guid.Empty)
            return _idPublicacionClase;
        using var fixture = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "NotificacionPreciosMuestra.pdf"));
        var bytes = new byte[fixture.Length];
        await fixture.ReadExactlyAsync(bytes);
        var texto = System.Text.Encoding.ASCII.GetString(bytes)
            .Replace("02/11/2025", "20/04/2025")
            .Replace("10/11/2025", "20/06/2025");
        var importar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, "/api/precios-alimentos/importar", tokenCaisy,
            new MultipartFormDataContent
            {
                { new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(texto)) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "archivo", "notificacion.pdf" },
            }));
        Assert.Equal(HttpStatusCode.Created, importar.StatusCode);
        var idPublicacion = Guid.Parse((await importar.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);
        var publicar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-alimentos/{idPublicacion}/publicar", tokenCaisy));
        Assert.Equal(HttpStatusCode.NoContent, publicar.StatusCode);
        _idPublicacionClase = idPublicacion;
        return idPublicacion;
    }

    private static DateOnly HoyBolivia() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));

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

    private static byte[] ImagenPng(int ancho = 8, int alto = 6)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        bitmap.Erase(SKColors.Gray);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 90);
        return datos.ToArray();
    }

    private static HttpContent ImagenMultipart(byte[] imagen, string nombre, Guid? reemplazaDocumentoId = null)
    {
        var cuerpo = new MultipartFormDataContent
        {
            { new ByteArrayContent(imagen) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }, "archivo", nombre },
        };
        if (reemplazaDocumentoId is { } previo)
            cuerpo.Add(new StringContent(previo.ToString()), "reemplazaDocumentoId");
        return cuerpo;
    }

    // Tenant nuevo con el módulo GestionAvicola (cupo semanal propio), cuenta
    // CAISY con la función del flujo, publicación vigente propia y un pedido
    // despachado con su nota registrada.
    private async Task<(HttpClient Cliente, string TokenTenant, string TokenCaisy, Guid IdPedido)>
        PrepararFlujoCompletoAsync()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);

        var emailTenant = $"nota-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/clientes", tokenAdmin,
            JsonContent.Create(new
            {
                razonSocial = "Avícola Nota S.A.C.",
                identificadorFiscal = $"4{Random.Shared.Next(100000000, 999999999)}",
                email = emailTenant,
                contrasena = IdentityFactory.ContrasenaDePrueba,
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var idCliente = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await anonimo.SendAsync(Pedido(
            HttpMethod.Put, $"/api/clientes/{idCliente}/modulos", tokenAdmin,
            JsonContent.Create(new { modulos = new[] { "GestionAvicola" } })));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);

        var emailCaisy = $"nota-caisy-{Guid.NewGuid():N}@icarus.test";
        var altaCaisy = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            })));
        Assert.Equal(HttpStatusCode.Created, altaCaisy.StatusCode);

        var cliente = _factory.CreateClient();
        var tokenTenant = await LoginComo(cliente, emailTenant);
        var tokenCaisy = await LoginComo(cliente, emailCaisy, "Clave-Caisy-123");

        // Publicación de la clase (una sola por corrida) para el envío.
        await AsegurarPublicacionDeClaseAsync(cliente, tokenCaisy);

        // Borrador, envío, aceptación y despacho del pedido de la prueba.
        var crear = await cliente.SendAsync(Pedido(HttpMethod.Post, "/api/pedidos-alimento", tokenTenant,
            JsonContent.Create(new
            {
                detalles = new[] { new { tipoAlimento = "PosturaUno", presentacion = "Bolsa", cantidad = 100 } },
            })));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var idPedido = Guid.Parse((await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        var enviar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/enviar", tokenTenant));
        Assert.Equal(HttpStatusCode.NoContent, enviar.StatusCode);
        var aceptar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/aceptar", tokenCaisy,
            JsonContent.Create(new { fechaEntregaEstimada = HoyBolivia().AddDays(3).ToString("yyyy-MM-dd") })));
        Assert.Equal(HttpStatusCode.NoContent, aceptar.StatusCode);
        var despachar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/despachar", tokenCaisy,
            JsonContent.Create(new
            {
                numeroNota = $"NOTA-{Guid.NewGuid():N}"[..20],
                fechaNota = HoyBolivia().ToString("yyyy-MM-dd"),
                totalInformado = (decimal?)18000m,
                lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadEntregada = 95 } },
            })));
        Assert.Equal(HttpStatusCode.NoContent, despachar.StatusCode);

        return (cliente, tokenTenant, tokenCaisy, idPedido);
    }

    private static async Task<Guid> SubirImagenAsync(
        HttpClient cliente, string tokenCaisy, Guid idPedido, byte[] imagen,
        Guid? reemplazaDocumentoId = null, string nombre = "nota-frente.png")
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos", tokenCaisy,
            ImagenMultipart(imagen, nombre, reemplazaDocumentoId)));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return Guid.Parse((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task LaDescargaQuedaAutorizadaConCabecerasSegurasY404ParaTerceros()
    {
        var (cliente, tokenTenant, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();
        var imagen = ImagenPng();
        var documentoId = await SubirImagenAsync(cliente, tokenCaisy, idPedido, imagen);

        // Vista derivada inline: se reencodifica, no son los bytes originales.
        var vistaCaisy = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos/{documentoId}/vista", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, vistaCaisy.StatusCode);
        Assert.Equal("image/jpeg", vistaCaisy.Content.Headers.ContentType!.MediaType);
        Assert.Contains("inline", vistaCaisy.Content.Headers.ContentDisposition!.ToString(), StringComparison.Ordinal);
        Assert.NotEqual(imagen, await vistaCaisy.Content.ReadAsByteArrayAsync());

        // Original como adjunto, idéntico al subido, con nosniff.
        var originalTenant = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}/nota/documentos/{documentoId}/original", tokenTenant));
        Assert.Equal(HttpStatusCode.OK, originalTenant.StatusCode);
        Assert.Equal("image/png", originalTenant.Content.Headers.ContentType!.MediaType);
        Assert.Contains("attachment", originalTenant.Content.Headers.ContentDisposition!.ToString(), StringComparison.Ordinal);
        Assert.Equal("nosniff", string.Join(",", originalTenant.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(imagen, await originalTenant.Content.ReadAsByteArrayAsync());

        // Un tercer tenant no descubre la existencia: 404 genérico en el grupo
        // del tenant; un token de tenant en el grupo de CAISY es 403.
        var tokenAjeno = await LoginComo(cliente, SemillaIdentidad.EmailClienteC1);
        var ajenoVista = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}/nota/documentos/{documentoId}/vista", tokenAjeno));
        var ajenoOriginal = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos/{documentoId}/original", tokenAjeno));
        Assert.Equal(HttpStatusCode.NotFound, ajenoVista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ajenoOriginal.StatusCode);

        // Sin sesión no hay descarga; documento inexistente responde 404.
        var anonima = await cliente.GetAsync(
            $"/api/pedidos-alimento/{idPedido}/nota/documentos/{documentoId}/vista");
        var inexistente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}/nota/documentos/{Guid.NewGuid()}/vista", tokenTenant));
        Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task LaSustitucionDesactivaElPrevioYConservaElHistorico()
    {
        var (cliente, tokenTenant, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();
        var previo = await SubirImagenAsync(cliente, tokenCaisy, idPedido, ImagenPng(), nombre: "borrosa.png");

        var nuevo = await SubirImagenAsync(
            cliente, tokenCaisy, idPedido, ImagenPng(9, 9), previo, "neta.png");

        // El histórico completo sigue descargable: los documentos publicados
        // son inmutables y la corrección solo los desactiva.
        var vistaPrevio = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}/nota/documentos/{previo}/vista", tokenTenant));
        var vistaNuevo = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos/{nuevo}/vista", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, vistaPrevio.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vistaNuevo.StatusCode);

        // El documento ya reemplazado no se reemplaza otra vez: 409, y un id
        // inexistente también es 409 (nada se escribe al volumen).
        var reemplazoDuplicado = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos", tokenCaisy,
            ImagenMultipart(ImagenPng(), "otra.png", previo)));
        var reemplazoInexistente = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos", tokenCaisy,
            ImagenMultipart(ImagenPng(), "otra.png", Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.Conflict, reemplazoDuplicado.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reemplazoInexistente.StatusCode);
    }

    [Fact]
    public async Task UnPedidoAjenoDevuelve404YUnaImagenFalsaSeRechaza()
    {
        var (cliente, _, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();

        // Pedido inexistente: 404 sin revelar datos.
        var ajeno = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{Guid.NewGuid()}/nota/documentos", tokenCaisy,
            ImagenMultipart(ImagenPng(), "x.png")));
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);

        // Imagen falsa (un PDF renombrado): la firma real la rechaza.
        var pdfFalso = "%PDF-1.7 esto no es una imagen"u8.ToArray();
        var falso = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/nota/documentos", tokenCaisy,
            ImagenMultipart(pdfFalso, "falsa.png")));
        Assert.Equal(HttpStatusCode.BadRequest, falso.StatusCode);
    }

    [Fact]
    public async Task UnSegundoDespachoDevuelveConflicto()
    {
        var (cliente, _, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();

        var segundo = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{idPedido}/despachar", tokenCaisy,
            JsonContent.Create(new
            {
                numeroNota = "NOTA-2",
                fechaNota = HoyBolivia().ToString("yyyy-MM-dd"),
                totalInformado = (decimal?)null,
                lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadEntregada = 100 } },
            })));

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
    }

    // SP8C Tarea 4 (spec: "Despacho, nota y recepción" y "Balance"): el tenant
    // compara solicitado/despachado, informa la cantidad realmente recibida por
    // línea y confirma el estado final; CAISY es notificada en la bandeja
    // global. El histórico expone la entrega (nota, respaldos, totales) y la
    // recepción con su snapshot de diferencias.
    [Fact]
    public async Task ElTenantConfirmaLaRecepcionYCaisyEsNotificada()
    {
        var (cliente, tokenTenant, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();
        await SubirImagenAsync(cliente, tokenCaisy, idPedido, ImagenPng());

        // Recepción de un tercero: 404 sin revelar existencia.
        var tokenAjeno = await LoginComo(cliente, SemillaIdentidad.EmailClienteC1);
        var ajeno = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/recibir", tokenAjeno,
            JsonContent.Create(new { lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadRecibida = 95 } } })));
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);

        // El tenant informa lo realmente recibido: conforme con lo despachado.
        var conforme = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/recibir", tokenTenant,
            JsonContent.Create(new { lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadRecibida = 95 } } })));
        Assert.Equal(HttpStatusCode.NoContent, conforme.StatusCode);

        // Reintento: 409 sin duplicar la transición.
        var reintento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/recibir", tokenTenant,
            JsonContent.Create(new { lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadRecibida = 95 } } })));
        Assert.Equal(HttpStatusCode.Conflict, reintento.StatusCode);

        // El histórico del tenant: entrega con nota, respaldos y totales, y
        // recepción conforme; solicitado vs despachado vs recibido contrastable.
        var detalle = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}", tokenTenant));
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        var cuerpo = await detalle.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RecibidoConforme", cuerpo.GetProperty("estado").GetString());
        var precioCongelado = cuerpo.GetProperty("lineas").EnumerateArray().Single()
            .GetProperty("precioFinalPor40Kg").GetDecimal();
        var entrega = cuerpo.GetProperty("entrega");
        Assert.Equal(95, entrega.GetProperty("lineas").EnumerateArray().Single()
            .GetProperty("cantidadEntregada").GetInt32());
        Assert.Equal(95m * precioCongelado, entrega.GetProperty("totalDespachado").GetDecimal());
        Assert.True(entrega.GetProperty("totalNetoInformado").GetDecimal() > 0);
        Assert.Equal(1, entrega.GetProperty("documentos").GetArrayLength());
        var recepcion = cuerpo.GetProperty("recepcion");
        Assert.Equal(95, recepcion.GetProperty("lineas").EnumerateArray().Single()
            .GetProperty("cantidadRecibida").GetInt32());
        Assert.Equal(95m * precioCongelado, recepcion.GetProperty("totalRecibido").GetDecimal());
        Assert.Equal(0, recepcion.GetProperty("diferencias").GetArrayLength());

        // La bandeja de CAISY recibe la notificación de recepción conforme.
        var bandeja = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy/notificaciones", tokenCaisy));
        var tipos = (await bandeja.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("tipo").GetString()).ToHashSet();
        Assert.Contains("RecepcionConforme", tipos);
    }

    [Fact]
    public async Task LaRecepcionConDiferenciasQuedaEnElHistoricoConSuSnapshot()
    {
        var (cliente, tokenTenant, tokenCaisy, idPedido) = await PrepararFlujoCompletoAsync();

        // Recibió una bolsa menos de lo despachado: diferencias explícitas.
        var conDiferencias = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/recibir", tokenTenant,
            JsonContent.Create(new { lineas = new[] { new { tipoAlimento = "PosturaUno", cantidadRecibida = 93 } } })));
        Assert.Equal(HttpStatusCode.NoContent, conDiferencias.StatusCode);

        var detalle = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{idPedido}", tokenTenant));
        var cuerpo = await detalle.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RecibidoConDiferencias", cuerpo.GetProperty("estado").GetString());
        var precioCongelado = cuerpo.GetProperty("lineas").EnumerateArray().Single()
            .GetProperty("precioFinalPor40Kg").GetDecimal();
        var recepcion = cuerpo.GetProperty("recepcion");
        var diferencia = Assert.Single(recepcion.GetProperty("diferencias").EnumerateArray());
        Assert.Equal(93, diferencia.GetProperty("cantidadRecibida").GetInt32());
        Assert.Equal(95, diferencia.GetProperty("cantidadEntregada").GetInt32());
        Assert.Equal(-2, diferencia.GetProperty("diferencia").GetInt32());
        // El total recibido usa lo realmente recibido y el precio congelado.
        Assert.Equal(93m * precioCongelado, recepcion.GetProperty("totalRecibido").GetDecimal());

        var bandeja = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy/notificaciones", tokenCaisy));
        var tipos = (await bandeja.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("tipo").GetString()).ToHashSet();
        Assert.Contains("RecepcionConDiferencias", tipos);
    }
}
