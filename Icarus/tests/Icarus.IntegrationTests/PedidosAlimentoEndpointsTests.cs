using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// SP8B Tarea 4 (spec: "Aplicaciones y autorización", "Máquina de estados" y
// "Límite semanal"): el tenant con la función PedidoAlimento gestiona sus
// borradores y envía; la cuenta CAISY con GestorPedidoAlimento procesa la
// bandeja global. Los reintentos y dobles clics devuelven 409 sin repetir la
// transición, sin duplicar notificaciones y sin gastar cupo. Los ids de otro
// tenant dan 404 genérico. Las pruebas comparten la base de la colección:
// cada prueba usa su propio tenant o borradores sin envío para no depender
// del orden, y el cupo semanal de cada tenant se agota solo en su prueba.
[Collection(IntegracionCollection.Nombre)]
public class PedidosAlimentoEndpointsTests
{
    private readonly IdentityFactory _factory;

    public PedidosAlimentoEndpointsTests(IdentityFactory factory) => _factory = factory;

    private static readonly DateOnly FechaDocumentoBase = new(2025, 1, 1);

    // Base distinta a la de PreciosAlimentosEndpointsTests: dos publicaciones
    // activas no pueden compartir vigencia y las clases comparten la base.
    private static int _secuencia;

    private static DateOnly VigenciaUnica() =>
        new DateOnly(2025, 7, 15).AddDays(Interlocked.Increment(ref _secuencia) * 30);

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

    private static JsonContent LineasJson(int cantidad = 100, string tipo = "PosturaUno") =>
        JsonContent.Create(new
        {
            detalles = new[]
            {
                new { tipoAlimento = tipo, presentacion = "Bolsa", cantidad },
            },
        });

    private static async Task<Guid> CrearBorradorAsync(HttpClient cliente, string token, int cantidad = 100)
    {
        var respuesta = await cliente.SendAsync(
            Pedido(HttpMethod.Post, "/api/pedidos-alimento", token, LineasJson(cantidad)));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(cuerpo.GetProperty("id").GetString()!);
    }

    private static async Task<HttpStatusCode> EnviarAsync(HttpClient cliente, string token, Guid id) =>
        (await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{id}/enviar", token))).StatusCode;

    private static async Task<JsonElement> ObtenerDetalleAsync(HttpClient cliente, string token, Guid id)
    {
        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento/{id}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(HttpClient Cliente, string Token)> CrearCuentaCaisyConFuncion()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"pedidos-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorPedidoAlimento" },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var token = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
        return (anonimo, token);
    }

    // Importa el PDF de muestra y lo publica con una vigencia propia: queda
    // como publicación vigente para los envíos de la prueba.
    private static async Task<Guid> ImportarYPublicarAsync(HttpClient cliente, string token)
    {
        var vigencia = VigenciaUnica();
        using var fixture = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "NotificacionPreciosMuestra.pdf"));
        var bytes = new byte[fixture.Length];
        await fixture.ReadExactlyAsync(bytes);
        var texto = System.Text.Encoding.ASCII.GetString(bytes)
            .Replace("02/11/2025", FechaDocumentoBase.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
            .Replace("10/11/2025", vigencia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

        var contenido = new MultipartFormDataContent();
        var original = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(texto));
        original.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        contenido.Add(original, "archivo", "notificacion.pdf");
        var alta = await cliente.SendAsync(Pedido(
            HttpMethod.Post, "/api/precios-alimentos/importar", token, contenido));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var idPublicacion = Guid.Parse((await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        var publicacion = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/precios-alimentos/{idPublicacion}/publicar", token));
        Assert.Equal(HttpStatusCode.NoContent, publicacion.StatusCode);
        return idPublicacion;
    }

    [Fact]
    public async Task ConsultasSinAutorizacionDevuelven401()
    {
        var cliente = _factory.CreateClient();

        var tenant = await cliente.GetAsync("/api/pedidos-alimento");
        var caisy = await cliente.GetAsync("/api/pedidos-alimento-caisy");

        Assert.Equal(HttpStatusCode.Unauthorized, tenant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, caisy.StatusCode);
    }

    [Fact]
    public async Task CuentasSinLaFuncionOSinTenantNoAcceden()
    {
        var cliente = _factory.CreateClient();
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);
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
        var tokenCaisySinFuncion = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);

        var trabajador = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/pedidos-alimento", tokenTrabajador));
        var caisySinFuncion = await anonimo.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy", tokenCaisySinFuncion));
        var clienteOk = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/pedidos-alimento", tokenCliente));
        var caisyConFuncion = await CrearCuentaCaisyConFuncion();
        var caisyOk = await caisyConFuncion.Cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy", caisyConFuncion.Token));

        Assert.Equal(HttpStatusCode.Forbidden, trabajador.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, caisySinFuncion.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clienteOk.StatusCode);
        Assert.Equal(HttpStatusCode.OK, caisyOk.StatusCode);
    }

    [Fact]
    public async Task LosIdsDeOtroTenantDanNoEncontradoSinRevelarDatos()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var tokenC1 = await LoginComo(cliente, SemillaIdentidad.EmailClienteC1);
        var idAjeno = await CrearBorradorAsync(cliente, tokenCliente);

        var detalle = await cliente.SendAsync(Pedido(HttpMethod.Get, $"/api/pedidos-alimento/{idAjeno}", tokenC1));
        var edicion = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/pedidos-alimento/{idAjeno}", tokenC1, LineasJson(200)));
        var borrado = await cliente.SendAsync(Pedido(
            HttpMethod.Delete, $"/api/pedidos-alimento/{idAjeno}", tokenC1));
        var envio = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idAjeno}/enviar", tokenC1));

        Assert.Equal(HttpStatusCode.NotFound, detalle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edicion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, envio.StatusCode);
    }

    [Fact]
    public async Task ElLimiteSemanalSeAplicaYElReenvioNoVuelveAConsumirCupo()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);

        var pedidos = new List<Guid>();
        for (var i = 0; i < 4; i++)
            pedidos.Add(await CrearBorradorAsync(cliente, tokenCliente));
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenCliente, pedidos[i]));
        // El cuarto envío supera el límite configurado (3) con un 409 genérico.
        Assert.Equal(HttpStatusCode.Conflict, await EnviarAsync(cliente, tokenCliente, pedidos[3]));

        // CAISY devuelve el primero y el tenant lo corrige y reenvía: mismo
        // pedido, el cupo no vuelve a consumirse y el cuarto sigue en 409.
        var devolucion = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidos[0]}/devolver", tokenCaisy,
            JsonContent.Create(new { motivo = "Revise las cantidades" })));
        Assert.Equal(HttpStatusCode.NoContent, devolucion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenCliente, pedidos[0]));
        Assert.Equal(HttpStatusCode.Conflict, await EnviarAsync(cliente, tokenCliente, pedidos[3]));

        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, pedidos[0]);
        Assert.Equal("Solicitado", detalle.GetProperty("estado").GetString());
        // Enviado, devuelto y reenviado: el mismo pedido conserva el historial.
        Assert.Equal(3, detalle.GetProperty("historial").GetArrayLength());
    }

    [Fact]
    public async Task LaBandejaCaisyFiltraYPagina()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
            ids.Add(await CrearBorradorAsync(cliente, tokenCliente));

        var pagina = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?estado=Borrador&pagina=1&tamanoPagina=2",
            tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var cuerpo = await pagina.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("total").GetInt32() >= 3);
        Assert.Equal(2, cuerpo.GetProperty("items").GetArrayLength());
        Assert.All(cuerpo.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("Borrador", item.GetProperty("estado").GetString());
            Assert.NotEqual(Guid.Empty, Guid.Parse(item.GetProperty("clienteId").GetString()!));
        });

        var porPresentacion = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?presentacion=Bolsa&pagina=1&tamanoPagina=100",
            tokenCaisy));
        Assert.True((await porPresentacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32() >= 3);

        // La bandeja es global: CAISY abre el detalle de un pedido ajeno.
        var detalle = await caisy.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{ids[0]}", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        var estadoInvalido = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy?estado=Inexistente", tokenCaisy));
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);
    }

    [Fact]
    public async Task LaBandejaDelTenantExponeCupoYPreciosVigentes()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailCliente);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);

        var cupo = await cliente.SendAsync(Pedido(HttpMethod.Get, "/api/pedidos-alimento/cupo", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, cupo.StatusCode);
        var cuerpoCupo = await cupo.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, cuerpoCupo.GetProperty("maximo").GetInt32());
        // El cupo compartido con otras pruebas no hace determinista el conteo:
        // se valida el rango, no el valor exacto.
        var enviados = cuerpoCupo.GetProperty("enviados").GetInt32();
        Assert.InRange(enviados, 0, 3);

        var vigente = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/precios-vigentes", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        var cuerpoVigente = await vigente.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Publicada", cuerpoVigente.GetProperty("estado").GetString());
        Assert.True(cuerpoVigente.GetProperty("detalles").GetArrayLength() > 0);

        // El trabajador sin la función no consulta el cupo.
        var tokenTrabajador = await LoginComo(cliente, SemillaIdentidad.EmailTrabajador);
        var prohibido = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/cupo", tokenTrabajador));
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
    }

    [Fact]
    public async Task FlujoCompletoConTransicionesRechazoYNotificaciones()
    {
        var cliente = _factory.CreateClient();
        var tokenC1 = await LoginComo(cliente, SemillaIdentidad.EmailClienteC1);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        var idPublicacion = await ImportarYPublicarAsync(caisy, tokenCaisy);

        // Los precios congelados salen de la publicación vigente en la fecha
        // de negocio, sin asumir cuál es la publicación más reciente.
        var vigente = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/precios-alimentos/vigente", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        var cuerpoVigente = await vigente.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(idPublicacion, Guid.Parse(cuerpoVigente.GetProperty("id").GetString()!));
        var precioPosturaUno = cuerpoVigente.GetProperty("detalles").EnumerateArray()
            .Single(d => d.GetProperty("tipoAlimento").GetString() == "PosturaUno"
                && d.GetProperty("presentacion").GetString() == "Bolsa")
            .GetProperty("precioFinalPor40Kg").GetDecimal();

        // Pedido A: alta inválida, creación, edición, envío y detalle congelado.
        var invalido = await cliente.SendAsync(Pedido(
            HttpMethod.Post, "/api/pedidos-alimento", tokenC1,
            JsonContent.Create(new
            {
                detalles = new[] { new { tipoAlimento = "Inexistente", presentacion = "Bolsa", cantidad = 10 } },
            })));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var pedidoA = await CrearBorradorAsync(cliente, tokenC1, 100);
        var detalleBorrador = await ObtenerDetalleAsync(cliente, tokenC1, pedidoA);
        Assert.Equal("Borrador", detalleBorrador.GetProperty("estado").GetString());
        Assert.Equal(0, detalleBorrador.GetProperty("historial").GetArrayLength());

        var edicion = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/pedidos-alimento/{pedidoA}", tokenC1, LineasJson(150)));
        Assert.Equal(HttpStatusCode.NoContent, edicion.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenC1, pedidoA));
        // Reintento de envío: segunda transición → 409, sin duplicar historial.
        Assert.Equal(HttpStatusCode.Conflict, await EnviarAsync(cliente, tokenC1, pedidoA));
        var reedicion = await cliente.SendAsync(Pedido(
            HttpMethod.Put, $"/api/pedidos-alimento/{pedidoA}", tokenC1, LineasJson(150)));
        Assert.Equal(HttpStatusCode.Conflict, reedicion.StatusCode);

        var detalleA = await ObtenerDetalleAsync(cliente, tokenC1, pedidoA);
        Assert.Equal("Solicitado", detalleA.GetProperty("estado").GetString());
        Assert.Equal(HoyBolivia().ToString("yyyy-MM-dd"), detalleA.GetProperty("fechaPedido").GetString());
        var linea = detalleA.GetProperty("lineas").EnumerateArray().Single();
        Assert.Equal(precioPosturaUno, linea.GetProperty("precioFinalPor40Kg").GetDecimal());
        Assert.Equal(precioPosturaUno * 150, linea.GetProperty("subtotalSolicitado").GetDecimal());
        Assert.NotNull(linea.GetProperty("notificacionPreciosAlimentosId").GetString());
        Assert.Equal(1, detalleA.GetProperty("historial").GetArrayLength());

        // Pedido B: devolución con motivo, reenvío, aceptación y entrega estimada.
        var pedidoB = await CrearBorradorAsync(cliente, tokenC1, 120);
        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenC1, pedidoB));

        var devolucionSinMotivo = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/devolver", tokenCaisy,
            JsonContent.Create(new { motivo = " " })));
        Assert.Equal(HttpStatusCode.BadRequest, devolucionSinMotivo.StatusCode);

        var devolucion = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/devolver", tokenCaisy,
            JsonContent.Create(new { motivo = "Falta el tipo de alimento correcto" })));
        Assert.Equal(HttpStatusCode.NoContent, devolucion.StatusCode);

        // Mientras el pedido está devuelto, la actualización de entrega no aplica.
        var etaSobreDevuelto = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/entrega-estimada", tokenCaisy,
            JsonContent.Create(new { fechaEntregaEstimada = HoyBolivia().AddDays(2).ToString("yyyy-MM-dd") })));
        Assert.Equal(HttpStatusCode.Conflict, etaSobreDevuelto.StatusCode);

        var detalleDevuelto = await ObtenerDetalleAsync(cliente, tokenC1, pedidoB);
        Assert.Equal("Borrador", detalleDevuelto.GetProperty("estado").GetString());
        Assert.Contains("Falta el tipo de alimento correcto",
            detalleDevuelto.GetProperty("historial").EnumerateArray()
                .Single(t => t.GetProperty("estadoDestino").GetString() == "Borrador")
                .GetProperty("motivo").GetString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenC1, pedidoB));

        var aceptacionPasada = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/aceptar", tokenCaisy,
            JsonContent.Create(new { fechaEntregaEstimada = HoyBolivia().AddDays(-1).ToString("yyyy-MM-dd") })));
        Assert.Equal(HttpStatusCode.BadRequest, aceptacionPasada.StatusCode);

        var aceptacion = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/aceptar", tokenCaisy,
            JsonContent.Create(new { fechaEntregaEstimada = HoyBolivia().AddDays(3).ToString("yyyy-MM-dd") })));
        Assert.Equal(HttpStatusCode.NoContent, aceptacion.StatusCode);

        var cambioEta = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoB}/entrega-estimada", tokenCaisy,
            JsonContent.Create(new { fechaEntregaEstimada = HoyBolivia().AddDays(10).ToString("yyyy-MM-dd") })));
        Assert.Equal(HttpStatusCode.NoContent, cambioEta.StatusCode);

        var detalleB = await ObtenerDetalleAsync(cliente, tokenC1, pedidoB);
        Assert.Equal("Aceptado", detalleB.GetProperty("estado").GetString());
        Assert.Equal(HoyBolivia().AddDays(10).ToString("yyyy-MM-dd"),
            detalleB.GetProperty("fechaEntregaEstimada").GetString());
        Assert.Equal(5, detalleB.GetProperty("historial").GetArrayLength());

        // Pedido C: rechazo con motivo, terminal.
        var pedidoC = await CrearBorradorAsync(cliente, tokenC1, 80);
        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenC1, pedidoC));
        var rechazoSinMotivo = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoC}/rechazar", tokenCaisy,
            JsonContent.Create(new { motivo = "" })));
        Assert.Equal(HttpStatusCode.BadRequest, rechazoSinMotivo.StatusCode);
        var rechazo = await caisy.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento-caisy/{pedidoC}/rechazar", tokenCaisy,
            JsonContent.Create(new { motivo = "Sin stock para esa presentación" })));
        Assert.Equal(HttpStatusCode.NoContent, rechazo.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, await EnviarAsync(cliente, tokenC1, pedidoC));
        var detalleC = await ObtenerDetalleAsync(cliente, tokenC1, pedidoC);
        Assert.Equal("Rechazado", detalleC.GetProperty("estado").GetString());

        // Notificaciones del tenant: la bandeja trae los eventos y el sondeo
        // responde 304 con el mismo ETag; la marca de lectura es idempotente.
        var sondeo = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/notificaciones", tokenC1));
        Assert.Equal(HttpStatusCode.OK, sondeo.StatusCode);
        var notificaciones = await sondeo.Content.ReadFromJsonAsync<JsonElement>();
        var tipos = notificaciones.GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("tipo").GetString()).ToHashSet();
        // El tenant ve las decisiones de CAISY, no sus propios envíos.
        Assert.Contains("PedidoDevuelto", tipos);
        Assert.Contains("PedidoAceptado", tipos);
        Assert.Contains("EntregaEstimadaActualizada", tipos);
        Assert.Contains("PedidoRechazado", tipos);
        Assert.DoesNotContain("PedidoSolicitado", tipos);
        Assert.True(notificaciones.GetProperty("contador").GetInt32() >= 4);
        var etag = sondeo.Headers.ETag!.ToString();

        var sondeoIgual = new HttpRequestMessage(HttpMethod.Get, "/api/pedidos-alimento/notificaciones");
        sondeoIgual.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenC1);
        sondeoIgual.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));
        var respuestaIgual = await cliente.SendAsync(sondeoIgual);
        Assert.Equal(HttpStatusCode.NotModified, respuestaIgual.StatusCode);

        var primera = notificaciones.GetProperty("items").EnumerateArray().First();
        var marcar = await cliente.SendAsync(Pedido(
            HttpMethod.Post,
            $"/api/pedidos-alimento/notificaciones/{primera.GetProperty("id").GetString()}/marcar-leida",
            tokenC1));
        Assert.Equal(HttpStatusCode.NoContent, marcar.StatusCode);

        var sondeoTrasLeer = await cliente.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento/notificaciones", tokenC1));
        var notificacionesTrasLeer = await sondeoTrasLeer.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(notificaciones.GetProperty("contador").GetInt32() - 1,
            notificacionesTrasLeer.GetProperty("contador").GetInt32());
        Assert.NotEqual(etag, sondeoTrasLeer.Headers.ETag!.ToString());

        // Bandeja global de CAISY: contiene los envíos y el reenvío.
        var bandejaCaisy = await caisy.SendAsync(Pedido(
            HttpMethod.Get, "/api/pedidos-alimento-caisy/notificaciones", tokenCaisy));
        Assert.Equal(HttpStatusCode.OK, bandejaCaisy.StatusCode);
        var tiposCaisy = (await bandejaCaisy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("tipo").GetString()).ToHashSet();
        Assert.Contains("PedidoSolicitado", tiposCaisy);
        Assert.Contains("PedidoReenviado", tiposCaisy);
        Assert.DoesNotContain("PedidoDevuelto", tiposCaisy);

        // El corte por fecha futura no trae elementos.
        var sondeoFuturo = await cliente.SendAsync(Pedido(
            HttpMethod.Get,
            $"/api/pedidos-alimento/notificaciones?since={DateTime.UtcNow.AddMinutes(5):O}", tokenC1));
        var cuerpoFuturo = await sondeoFuturo.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, cuerpoFuturo.GetProperty("items").GetArrayLength());
    }
}
