using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.Identity.Infrastructure;
using Serilog.Formatting.Compact;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

// Centinelas sintéticos: si cualquiera aparece en un log, hay fuga. No son
// datos reales de ninguna persona.
[Collection(IntegracionCollection.Nombre)]
public sealed class PrivacidadAccesoTests
{
    private const string CentinelaRostro = "SENTINELA_ROSTRO_ZZZ";
    private const string CentinelaMotivo = "SENTINELA_MOTIVO_ZZZ";
    private const string NombreTrabajador = "Nombre Ficticio Centinela";

    private readonly IdentityFactory _factory;

    public PrivacidadAccesoTests(IdentityFactory factory) => _factory = factory;

    private static string Base64(string contenido) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(contenido));

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private async Task<string> LoginComoAsync(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
    }

    private async Task<(Guid ClienteId, string Email, string Token)> CrearClienteConModuloAsync()
    {
        var admin = await LoginComoAsync(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"ca-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(HttpMethod.Post, "/api/clientes", admin);
        alta.Content = JsonContent.Create(new
        {
            razonSocial = "Cliente Privacidad S.A.C.",
            identificadorFiscal = $"2{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await clienteHttp.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var clienteId = (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var asignar = PedidoAutenticado(HttpMethod.Put, $"/api/clientes/{clienteId}/modulos", admin);
        asignar.Content = JsonContent.Create(new { modulos = new[] { "ControlAcceso" } });
        Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(asignar)).StatusCode);

        return (clienteId, email, await LoginComoAsync(email));
    }

    private async Task<Guid> CrearTrabajadorAsync(Guid clienteId, string token)
    {
        var cliente = _factory.CreateClient();
        var email = $"ta-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", token);
        alta.Content = JsonContent.Create(new
        {
            nombre = NombreTrabajador,
            documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuesta = await cliente.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task NingunaMuestraNiMotivoApareceEnLosLogsDelPipeline()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        // Solo interesa lo que registra el flujo biométrico a partir de aquí.
        IdentityFactory.Colector.Limpiar();

        // Enrolamiento con muestra centinela.
        var enrolar = _factory.CreateClient();
        var pedidoEnrolar = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/enrolamiento", token);
        pedidoEnrolar.Content = JsonContent.Create(new
        {
            muestraBase64 = Base64($"{CentinelaRostro}-ENROL"),
            formato = "jpg",
            claveIdempotencia = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.OK, (await enrolar.SendAsync(pedidoEnrolar)).StatusCode);

        // Activación del kiosco y marcación exitosa con muestra centinela.
        var kiosco = _factory.CreateClient();
        var activar = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email, contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        activar.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuestaActivacion = await kiosco.SendAsync(activar);
        Assert.Equal(HttpStatusCode.NoContent, respuestaActivacion.StatusCode);
        var cookie = respuestaActivacion.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(OpcionesKiosco.Cookie + "=", StringComparison.Ordinal))
            .Split(';')[0];

        async Task<HttpResponseMessage> MarcarAsync(string muestra)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/marcaciones")
            {
                Content = JsonContent.Create(new
                {
                    accion = "Entrada",
                    muestraBase64 = Base64(muestra),
                    formato = "jpg",
                    claveIdempotencia = Guid.NewGuid(),
                }),
            };
            pedido.Headers.Add("Cookie", cookie);
            pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
            return await kiosco.SendAsync(pedido);
        }

        Assert.Equal(HttpStatusCode.OK, (await MarcarAsync($"{CentinelaRostro}-ENROL")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MarcarAsync($"{CentinelaRostro}-NEGATIVO")).StatusCode);

        // Registro manual y corrección con motivo centinela.
        var manual = PedidoAutenticado(
            HttpMethod.Post, "/api/control-acceso/marcaciones/manuales", token);
        manual.Content = JsonContent.Create(new
        {
            trabajadorId,
            tipo = "Salida",
            horaDeclaradaUtc = DateTimeOffset.UtcNow,
            motivo = $"{CentinelaMotivo}-MANUAL",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuestaManual = await _factory.CreateClient().SendAsync(manual);
        Assert.Equal(HttpStatusCode.Created, respuestaManual.StatusCode);
        var jornadaId = (await respuestaManual.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("jornadaId").GetGuid();

        var detalle = await _factory.CreateClient().SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/api/control-acceso/jornadas/{jornadaId}", token));
        var version = (await detalle.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt32();

        var corregir = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/jornadas/{jornadaId}/correcciones", token);
        corregir.Content = JsonContent.Create(new
        {
            versionEsperada = version,
            motivo = $"{CentinelaMotivo}-CORRECCION",
            valores = new[] { new { tipo = "Entrada", instanteUtc = DateTimeOffset.UtcNow.AddHours(-2) } },
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CreateClient().SendAsync(corregir)).StatusCode);

        var registros = SerializarTodo();
        Assert.DoesNotContain(CentinelaRostro, registros);
        Assert.DoesNotContain(Base64($"{CentinelaRostro}-ENROL"), registros);
        Assert.DoesNotContain(CentinelaMotivo, registros);
        Assert.DoesNotContain(NombreTrabajador, registros);
    }

    [Fact]
    public async Task IncidenciasYNotificacionesNoFiltranDatosSensibles()
    {
        var (clienteId, email, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        await EnrolarTrabajadorAsync(token, trabajadorId);

        var kiosco = _factory.CreateClient();
        var activar = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/sesion")
        {
            Content = JsonContent.Create(new { email, contrasena = IdentityFactory.ContrasenaDePrueba }),
        };
        activar.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
        var respuestaActivacion = await kiosco.SendAsync(activar);
        Assert.Equal(HttpStatusCode.NoContent, respuestaActivacion.StatusCode);
        var cookie = respuestaActivacion.Headers.GetValues("Set-Cookie")
            .Single(h => h.StartsWith(OpcionesKiosco.Cookie + "=", StringComparison.Ordinal))
            .Split(';')[0];

        async Task<JsonElement> CapturarAsync(string muestra, Guid? flujoId = null)
        {
            var cuerpo = new Dictionary<string, object>
            {
                ["accion"] = "Entrada",
                ["muestraBase64"] = Base64(muestra),
                ["formato"] = "jpg",
                ["claveIdempotencia"] = Guid.NewGuid(),
            };
            if (flujoId.HasValue)
                cuerpo["flujoId"] = flujoId.Value;

            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/control-acceso/kiosco/marcaciones")
            {
                Content = JsonContent.Create(cuerpo),
            };
            pedido.Headers.Add("Cookie", cookie);
            pedido.Headers.Add(OpcionesKiosco.EncabezadoAntiforgery, "1");
            var respuesta = await kiosco.SendAsync(pedido);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        }

        // Limpieza justo antes del flujo de incidencias.
        IdentityFactory.Colector.Limpiar();

        var primera = await CapturarAsync("SIN_ROSTRO");
        Assert.Equal("Rechazada", primera.GetProperty("estado").GetString());
        var flujoId = primera.GetProperty("flujoId").GetGuid();
        var segunda = await CapturarAsync("VARIOS_ROSTROS", flujoId);
        Assert.Equal("Rechazada", segunda.GetProperty("estado").GetString());
        var tercera = await CapturarAsync("PAD_FALLA", flujoId);
        Assert.Equal("Incidencia", tercera.GetProperty("estado").GetString());

        // Lista y notificaciones no exponen identidad ni evidencia facial.
        var listarIncidencias = PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/incidencias", token);
        var respuestaIncidencias = await _factory.CreateClient().SendAsync(listarIncidencias);
        Assert.Equal(HttpStatusCode.OK, respuestaIncidencias.StatusCode);
        var jsonIncidencias = await respuestaIncidencias.Content.ReadAsStringAsync();
        Assert.DoesNotContain(NombreTrabajador, jsonIncidencias);
        Assert.DoesNotContain(CentinelaRostro, jsonIncidencias);
        var incidenciaId = (await respuestaIncidencias.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("id").GetGuid();

        var listarNotificaciones = PedidoAutenticado(HttpMethod.Get, "/api/control-acceso/notificaciones", token);
        var respuestaNotificaciones = await _factory.CreateClient().SendAsync(listarNotificaciones);
        Assert.Equal(HttpStatusCode.OK, respuestaNotificaciones.StatusCode);
        var jsonNotificaciones = await respuestaNotificaciones.Content.ReadAsStringAsync();
        Assert.DoesNotContain(NombreTrabajador, jsonNotificaciones);
        Assert.DoesNotContain(CentinelaRostro, jsonNotificaciones);

        // Resolución con motivo centinela.
        var resolver = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/incidencias/{incidenciaId}/resolucion", token);
        resolver.Content = JsonContent.Create(new
        {
            trabajadorId,
            tipo = "Entrada",
            horaDeclaradaUtc = DateTimeOffset.UtcNow.AddHours(-1),
            motivo = $"{CentinelaMotivo}-INCIDENCIA",
            claveIdempotencia = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CreateClient().SendAsync(resolver)).StatusCode);

        var registros = SerializarTodo();
        Assert.DoesNotContain(CentinelaRostro, registros);
        Assert.DoesNotContain(CentinelaMotivo, registros);
        Assert.DoesNotContain(NombreTrabajador, registros);
    }

    private async Task EnrolarTrabajadorAsync(string token, Guid trabajadorId)
    {
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/trabajadores/{trabajadorId}/enrolamiento", token);
        pedido.Content = JsonContent.Create(new
        {
            muestraBase64 = Base64($"{CentinelaRostro}-ENROL"),
            formato = "jpg",
            claveIdempotencia = Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private static string SerializarTodo()
    {
        using var escritor = new StringWriter();
        foreach (var evento in IdentityFactory.Colector.Eventos)
            new CompactJsonFormatter().Format(evento, escritor);
        return escritor.ToString();
    }
}
