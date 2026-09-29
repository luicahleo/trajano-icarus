using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests.ControlAcceso;

[Collection(IntegracionCollection.Nombre)]
public class HistorialCorreccionesTests
{
    private readonly IdentityFactory _factory;

    public HistorialCorreccionesTests(IdentityFactory factory) => _factory = factory;

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
            razonSocial = "Cliente Historial S.A.C.",
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

    private async Task<Guid> CrearTrabajadorAsync(Guid clienteId, string tokenCliente)
    {
        var cliente = _factory.CreateClient();
        var email = $"ta-{Guid.NewGuid():N}@icarus.test";
        var alta = PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", tokenCliente);
        alta.Content = JsonContent.Create(new
        {
            nombre = "Nombre Ficticio",
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

    private async Task<Guid> RegistrarManualAsync(
        string token, Guid trabajadorId, string tipo, DateTimeOffset horaDeclarada,
        string motivo = "Falla de reconocimiento", Guid? clave = null)
    {
        var cliente = _factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, "/api/control-acceso/marcaciones/manuales", token);
        pedido.Content = JsonContent.Create(new
        {
            trabajadorId,
            tipo,
            horaDeclaradaUtc = horaDeclarada,
            motivo,
            claveIdempotencia = clave ?? Guid.NewGuid(),
        });
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jornadaId").GetGuid();
    }

    private Task<Guid> RegistrarEntradaManualAsync(
        string token, Guid trabajadorId, DateTimeOffset horaDeclarada,
        string motivo = "Falla de reconocimiento") =>
        RegistrarManualAsync(token, trabajadorId, "Entrada", horaDeclarada, motivo);

    private async Task<JsonElement> ObtenerJornadaAsync(string token, Guid jornadaId)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/api/control-acceso/jornadas/{jornadaId}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> CorregirAsync(
        IdentityFactory factory, string token, Guid jornadaId, int version, string motivo,
        params (string Tipo, DateTimeOffset Instante)[] valores)
    {
        var cliente = factory.CreateClient();
        var pedido = PedidoAutenticado(
            HttpMethod.Post, $"/api/control-acceso/jornadas/{jornadaId}/correcciones", token);
        pedido.Content = JsonContent.Create(new
        {
            versionEsperada = version,
            motivo,
            valores = valores.Select(v => new { tipo = v.Tipo, instanteUtc = v.Instante }),
        });
        return await cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task CorregirConservaOriginalesYCreaRevision()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var entrada = DateTimeOffset.UtcNow.AddHours(-3);
        var jornadaId = await RegistrarEntradaManualAsync(token, trabajadorId, entrada);

        var antes = await ObtenerJornadaAsync(token, jornadaId);
        var version = antes.GetProperty("version").GetInt32();
        var corregida = entrada.AddMinutes(30);

        var respuesta = await CorregirAsync(
            _factory, token, jornadaId, version, "Ajuste autorizado", ("Entrada", corregida));
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var despues = await ObtenerJornadaAsync(token, jornadaId);
        Assert.Single(despues.GetProperty("marcaciones").EnumerateArray());
        Assert.Equal(
            entrada.ToUnixTimeSeconds(),
            despues.GetProperty("marcaciones")[0].GetProperty("instanteUtc").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.Single(despues.GetProperty("revisiones").EnumerateArray());
        var efectivos = despues.GetProperty("valoresEfectivos").EnumerateArray().ToList();
        Assert.Single(efectivos);
        Assert.Equal(
            corregida.ToUnixTimeSeconds(),
            efectivos[0].GetProperty("instanteUtc").GetDateTimeOffset().ToUnixTimeSeconds());
    }

    [Fact]
    public async Task CorregirConVersionObsoletaDevuelve409()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var entrada = DateTimeOffset.UtcNow.AddHours(-3);
        var jornadaId = await RegistrarEntradaManualAsync(token, trabajadorId, entrada);
        var version = (await ObtenerJornadaAsync(token, jornadaId)).GetProperty("version").GetInt32();

        var primera = await CorregirAsync(
            _factory, token, jornadaId, version, "Primera", ("Entrada", entrada));
        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);

        var obsoleta = await CorregirAsync(
            _factory, token, jornadaId, version, "Obsoleta", ("Entrada", entrada));
        Assert.Equal(HttpStatusCode.Conflict, obsoleta.StatusCode);
    }

    [Fact]
    public async Task AnularJornadaConValoresVaciosConservaOriginales()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var jornadaId = await RegistrarEntradaManualAsync(
            token, trabajadorId, DateTimeOffset.UtcNow.AddHours(-2));
        var version = (await ObtenerJornadaAsync(token, jornadaId)).GetProperty("version").GetInt32();

        var respuesta = await CorregirAsync(_factory, token, jornadaId, version, "Jornada anulada");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var despues = await ObtenerJornadaAsync(token, jornadaId);
        Assert.Empty(despues.GetProperty("valoresEfectivos").EnumerateArray());
        Assert.Single(despues.GetProperty("marcaciones").EnumerateArray());
    }

    [Fact]
    public async Task CorreccionHaciaElFuturoEsRechazada()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        var jornadaId = await RegistrarEntradaManualAsync(
            token, trabajadorId, DateTimeOffset.UtcNow.AddHours(-2));
        var version = (await ObtenerJornadaAsync(token, jornadaId)).GetProperty("version").GetInt32();

        var respuesta = await CorregirAsync(
            _factory, token, jornadaId, version, "Futuro", ("Salida", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task JornadaDeOtroTenantNoEsVisible()
    {
        var (clienteA, _, tokenA) = await CrearClienteConModuloAsync();
        var trabajadorA = await CrearTrabajadorAsync(clienteA, tokenA);
        var jornadaA = await RegistrarEntradaManualAsync(
            tokenA, trabajadorA, DateTimeOffset.UtcNow.AddHours(-2));
        var (_, _, tokenB) = await CrearClienteConModuloAsync();

        var cliente = _factory.CreateClient();
        var detalle = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, $"/api/control-acceso/jornadas/{jornadaA}", tokenB));
        Assert.Equal(HttpStatusCode.NotFound, detalle.StatusCode);

        var lista = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/api/control-acceso/jornadas", tokenB));
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var total = (await lista.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32();
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task ListarJornadasRespetaPaginacionDelTenant()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajador1 = await CrearTrabajadorAsync(clienteId, token);
        var trabajador2 = await CrearTrabajadorAsync(clienteId, token);
        await RegistrarEntradaManualAsync(token, trabajador1, DateTimeOffset.UtcNow.AddHours(-2));
        await RegistrarEntradaManualAsync(token, trabajador2, DateTimeOffset.UtcNow.AddHours(-3));

        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(PedidoAutenticado(
            HttpMethod.Get, "/api/control-acceso/jornadas?pagina=1&tamanoPagina=1", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, cuerpo.GetProperty("total").GetInt32());
        Assert.Single(cuerpo.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task CorreccionExigeJornadaExistente()
    {
        var (_, _, token) = await CrearClienteConModuloAsync();

        var respuesta = await CorregirAsync(
            _factory, token, Guid.NewGuid(), 1, "Inexistente", ("Entrada", DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task MarcacionPosteriorALaRevisionParticipaEnLaSecuenciaEfectiva()
    {
        var (clienteId, _, token) = await CrearClienteConModuloAsync();
        var trabajadorId = await CrearTrabajadorAsync(clienteId, token);
        // 16:00 UTC del día anterior equivale a mediodía en Bolivia y evita que
        // el intervalo de tres horas cruce el cambio de día civil boliviano.
        var entrada = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(16), TimeSpan.Zero);
        var jornadaId = await RegistrarEntradaManualAsync(token, trabajadorId, entrada);
        var version = (await ObtenerJornadaAsync(token, jornadaId)).GetProperty("version").GetInt32();

        var corregida = entrada.AddMinutes(30);
        var correccion = await CorregirAsync(
            _factory, token, jornadaId, version, "Ajuste", ("Entrada", corregida));
        Assert.Equal(HttpStatusCode.NoContent, correccion.StatusCode);

        await RegistrarManualAsync(token, trabajadorId, "Salida", entrada.AddHours(3));

        var detalle = await ObtenerJornadaAsync(token, jornadaId);
        var efectivos = detalle.GetProperty("valoresEfectivos").EnumerateArray().ToList();
        Assert.Equal(2, efectivos.Count);
        Assert.Equal(
            corregida.ToUnixTimeSeconds(),
            efectivos[0].GetProperty("instanteUtc").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.Equal("Salida", efectivos[1].GetProperty("tipo").GetString());
        Assert.Equal(2, detalle.GetProperty("marcaciones").GetArrayLength());
    }
}
