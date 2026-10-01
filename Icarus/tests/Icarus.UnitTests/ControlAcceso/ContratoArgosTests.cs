using System.Text;
using System.Net;
using System.Text.Json;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Infrastructure;
using Icarus.ControlAcceso.Infrastructure.Argos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Icarus.UnitTests.ControlAcceso;

public class ContratoArgosTests
{
    private readonly ProveedorIdentidadFacialDoble _proveedor = new();

    private static MuestraFacial Muestra(string contenido) =>
        new(Encoding.ASCII.GetBytes(contenido), "jpg");

    [Fact]
    public async Task ClienteArgos_Extraer_mapea_respuesta_exitosa_y_envia_contrato_v2()
    {
        HttpRequestMessage? solicitud = null;
        var handler = new TestHandler(request =>
        {
            solicitud = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"exitoso":true,"vector":[0.1,-0.2],"modelo_formato":"arcface-cosine-512","version_modelo":1,"pad_aprobado":true}""")
            };
        });
        var cliente = new ClienteArgosControlAcceso(
            new HttpClient(handler),
            Options.Create(new OpcionesArgosControlAcceso { Url = "http://argos", ApiKey = "clave-prueba" }));

        var imagen = new byte[] { 1, 2, 3 };
        var resultado = await cliente.ExtraerAsync(new MuestraFacial(imagen, "jpeg"));

        Assert.True(resultado.Exitoso);
        Assert.Equal("arcface-cosine-512", resultado.ModeloFormato);
        Assert.Equal(1, resultado.VersionModelo);
        Assert.Equal(new[] { 0.1f, -0.2f }.SelectMany(BitConverter.GetBytes), resultado.Vector);
        Assert.NotNull(solicitud);
        Assert.Equal(HttpMethod.Post, solicitud.Method);
        Assert.Equal("/api/v2/control-acceso/extracciones", solicitud.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", solicitud.Headers.Authorization!.Scheme);
        Assert.Equal("clave-prueba", solicitud.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(await solicitud.Content!.ReadAsStringAsync());
        Assert.Equal("trajano-icarus-control-acceso", body.RootElement.GetProperty("aplicacion").GetString());
        Assert.Equal(Guid.Empty.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(Convert.ToBase64String(imagen), body.RootElement.GetProperty("imagen").GetString());
        Assert.Equal("jpeg", body.RootElement.GetProperty("formato").GetString());
    }

    [Fact]
    public async Task ClienteArgos_Identificar_mapea_solo_trabajador_y_envia_candidatos_v2()
    {
        HttpRequestMessage? solicitud = null;
        var trabajadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new TestHandler(request =>
        {
            solicitud = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"identificado":true,"trabajador_id":"11111111-1111-1111-1111-111111111111","modelo_formato":"arcface-cosine-512","version_modelo":1,"pad_aprobado":true,"similitud":0.99}""")
            };
        });
        var cliente = new ClienteArgosControlAcceso(
            new HttpClient(handler),
            Options.Create(new OpcionesArgosControlAcceso { Url = "http://argos", ApiKey = "clave-prueba" }));
        var candidatos = new List<CandidatoFacial>
        {
            new(trabajadorId, new float[] { 0.1f, -0.2f }.SelectMany(BitConverter.GetBytes).ToArray(), "arcface-cosine-512", 1, 7)
        };

        var resultado = await cliente.IdentificarAsync(new MuestraFacial(new byte[] { 1, 2 }, "jpeg"), candidatos);

        Assert.True(resultado.Identificado);
        Assert.Equal(trabajadorId, resultado.TrabajadorId);
        Assert.NotNull(solicitud);
        Assert.Equal(HttpMethod.Post, solicitud.Method);
        Assert.Equal("/api/v2/control-acceso/identificaciones", solicitud.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(await solicitud.Content!.ReadAsStringAsync());
        Assert.Equal("arcface-cosine-512", body.RootElement.GetProperty("modelo_formato_esperado").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("version_modelo_esperada").GetInt32());
        var candidato = Assert.Single(body.RootElement.GetProperty("candidatos").EnumerateArray());
        Assert.Equal(trabajadorId.ToString(), candidato.GetProperty("trabajador_id").GetString());
        Assert.Equal(7, candidato.GetProperty("version_enrolamiento").GetInt32());
        Assert.Equal(new[] { (double)0.1f, (double)-0.2f }, candidato.GetProperty("vector").EnumerateArray().Select(x => x.GetDouble()));
    }

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    [Fact]
    public async Task ExtraccionDevuelveVectorConModeloExplicito()
    {
        var resultado = await _proveedor.ExtraerAsync(Muestra("rostro-sintetico-1"));

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Vector);
        Assert.Equal("doble-v1", resultado.ModeloFormato);
        Assert.True(resultado.VersionModelo > 0);
    }

    [Theory]
    [InlineData("PAD_FALLA", "pad_fallido")]
    [InlineData("SIN_ROSTRO", "sin_rostro")]
    [InlineData("VARIOS_ROSTROS", "varios_rostros")]
    public async Task ExtraccionRechazaPruebaDeVidaYRostrosInvalidos(string contenido, string motivo)
    {
        var resultado = await _proveedor.ExtraerAsync(Muestra(contenido));

        Assert.False(resultado.Exitoso);
        Assert.Null(resultado.Vector);
        Assert.Equal(motivo, resultado.Motivo);
    }

    [Fact]
    public async Task IdentificacionCoincideSoloConCandidatoDelMismoVector()
    {
        var trabajador = Guid.NewGuid();
        var candidatos = new[]
        {
            new CandidatoFacial(trabajador, Encoding.ASCII.GetBytes("rostro-A"), "doble-v1", 1, 1),
            new CandidatoFacial(Guid.NewGuid(), Encoding.ASCII.GetBytes("rostro-B"), "doble-v1", 1, 1),
        };

        var coincide = await _proveedor.IdentificarAsync(Muestra("rostro-B"), candidatos);
        var sinCoincidencia = await _proveedor.IdentificarAsync(Muestra("rostro-C"), candidatos);

        Assert.True(coincide.Identificado);
        Assert.Equal(candidatos[1].TrabajadorId, coincide.TrabajadorId);
        Assert.False(sinCoincidencia.Identificado);
        Assert.Equal("sin_coincidencia", sinCoincidencia.Motivo);
    }

    [Fact]
    public async Task IdentificacionConVariosCandidatosIgualesEsAmbigua()
    {
        var vector = Encoding.ASCII.GetBytes("rostro-repetido");
        var candidatos = new[]
        {
            new CandidatoFacial(Guid.NewGuid(), vector, "doble-v1", 1, 1),
            new CandidatoFacial(Guid.NewGuid(), vector, "doble-v1", 1, 1),
        };

        var resultado = await _proveedor.IdentificarAsync(Muestra("rostro-repetido"), candidatos);

        Assert.False(resultado.Identificado);
        Assert.Equal("ambigua", resultado.Motivo);
    }

    [Fact]
    public async Task IdentificacionConModeloIncompatibleNoCoincide()
    {
        var candidatos = new[]
        {
            new CandidatoFacial(Guid.NewGuid(), Encoding.ASCII.GetBytes("rostro-A"), "otro-modelo", 9, 1),
        };

        var resultado = await _proveedor.IdentificarAsync(Muestra("rostro-A"), candidatos);

        Assert.False(resultado.Identificado);
        Assert.Equal("modelo_incompatible", resultado.Motivo);
    }

    [Fact]
    public async Task IdentificacionSinCandidatosNoCoincide()
    {
        var resultado = await _proveedor.IdentificarAsync(Muestra("rostro-A"), []);

        Assert.False(resultado.Identificado);
    }

    [Fact]
    public void CuandoNoHayUrl_UsaProveedorNoDisponible()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var servicios = new ServiceCollection();
        servicios.AddControlAccesoInfrastructure(configuracion);

        using var proveedorServicios = servicios.BuildServiceProvider();

        Assert.IsType<ProveedorIdentidadFacialNoDisponible>(
            proveedorServicios.GetRequiredService<IProveedorIdentidadFacial>());
        Assert.Equal(TimeSpan.FromSeconds(15),
            proveedorServicios.GetRequiredService<IOptions<OpcionesArgosControlAcceso>>().Value.Timeout);
    }

    [Fact]
    public void CuandoHayUrl_ResuelveClienteYVinculaOpciones()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpcionesArgosControlAcceso.Seccion}:Url"] = "http://argos.example",
                [$"{OpcionesArgosControlAcceso.Seccion}:ApiKey"] = "clave-de-prueba",
                [$"{OpcionesArgosControlAcceso.Seccion}:Timeout"] = "00:00:09"
            })
            .Build();
        var servicios = new ServiceCollection();
        servicios.AddControlAccesoInfrastructure(configuracion);

        using var proveedorServicios = servicios.BuildServiceProvider();

        Assert.IsType<ClienteArgosControlAcceso>(
            proveedorServicios.GetRequiredService<IProveedorIdentidadFacial>());
        var opciones = proveedorServicios.GetRequiredService<IOptions<OpcionesArgosControlAcceso>>().Value;
        Assert.Equal("http://argos.example", opciones.Url);
        Assert.Equal("clave-de-prueba", opciones.ApiKey);
        Assert.Equal(TimeSpan.FromSeconds(9), opciones.Timeout);
    }

    [Fact]
    public void CuandoSeSolicitaDoble_PriorizaElDobleAunqueHayaUrl()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpcionesBiometria.Seccion}:UsarDoble"] = "true",
                [$"{OpcionesArgosControlAcceso.Seccion}:Url"] = "http://argos.example"
            })
            .Build();
        var servicios = new ServiceCollection();
        servicios.AddControlAccesoInfrastructure(configuracion);

        using var proveedorServicios = servicios.BuildServiceProvider();

        Assert.IsType<ProveedorIdentidadFacialDoble>(
            proveedorServicios.GetRequiredService<IProveedorIdentidadFacial>());
    }
}
