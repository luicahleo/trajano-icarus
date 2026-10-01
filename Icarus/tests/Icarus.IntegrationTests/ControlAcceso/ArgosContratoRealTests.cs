using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Infrastructure.Argos;
using Microsoft.Extensions.Options;

namespace Icarus.IntegrationTests.ControlAcceso;

public sealed class ArgosContratoRealTests
{
    [Fact]
    public async Task Cliente_serializa_extraccion_e_identificacion_segun_contrato_v2()
    {
        await using var argos = await ArgosHttpDoble.IniciarAsync();
        using var http = new HttpClient { BaseAddress = argos.BaseAddress };
        var apiKeyDePrueba = "clave-sintetica-de-integracion";
        var proveedor = new ClienteArgosControlAcceso(http,
            Options.Create(new OpcionesArgosControlAcceso
            {
                Url = argos.BaseAddress.ToString(),
                ApiKey = apiKeyDePrueba,
            }));
        var muestra = new MuestraFacial(Encoding.ASCII.GetBytes("imagen-sintetica"), "jpeg");

        var extraccion = await proveedor.ExtraerAsync(muestra);

        Assert.True(extraccion.Exitoso);
        Assert.Equal("arcface-cosine-512", extraccion.ModeloFormato);
        Assert.Equal(2, extraccion.VersionModelo);
        using var solicitudExtraccion = JsonDocument.Parse(await argos.CuerpoRecibidoAsync(
            "/api/v2/control-acceso/extracciones"));
        var cuerpoExtraccion = solicitudExtraccion.RootElement;
        Assert.Equal("trajano-icarus-control-acceso", cuerpoExtraccion.GetProperty("aplicacion").GetString());
        Assert.Equal(Guid.Empty, cuerpoExtraccion.GetProperty("tenant_id").GetGuid());
        Assert.Equal(Convert.ToBase64String(muestra.Contenido), cuerpoExtraccion.GetProperty("imagen").GetString());
        Assert.Equal("jpeg", cuerpoExtraccion.GetProperty("formato").GetString());
        Assert.Equal($"Bearer {apiKeyDePrueba}", await argos.AutorizacionRecibidaAsync(
            "/api/v2/control-acceso/extracciones"));

        var trabajadorId = Guid.Parse("31b806bc-3ac3-4a0e-a0a1-9d18129f53ca");
        var vector = new[] { 0.125f, -0.25f, 0.5f };
        var candidato = new CandidatoFacial(trabajadorId,
            vector.SelectMany(BitConverter.GetBytes).ToArray(), "arcface-cosine-512", 2, 7);
        var identificacion = await proveedor.IdentificarAsync(muestra, [candidato]);

        Assert.True(identificacion.Identificado);
        Assert.Equal(trabajadorId, identificacion.TrabajadorId);
        using var solicitudIdentificacion = JsonDocument.Parse(await argos.CuerpoRecibidoAsync(
            "/api/v2/control-acceso/identificaciones"));
        var cuerpoIdentificacion = solicitudIdentificacion.RootElement;
        Assert.Equal("trajano-icarus-control-acceso", cuerpoIdentificacion.GetProperty("aplicacion").GetString());
        Assert.Equal(Guid.Empty, cuerpoIdentificacion.GetProperty("tenant_id").GetGuid());
        Assert.Equal(Convert.ToBase64String(muestra.Contenido), cuerpoIdentificacion.GetProperty("imagen").GetString());
        Assert.Equal("jpeg", cuerpoIdentificacion.GetProperty("formato").GetString());
        Assert.Equal("arcface-cosine-512", cuerpoIdentificacion.GetProperty("modelo_formato_esperado").GetString());
        Assert.Equal(1, cuerpoIdentificacion.GetProperty("version_modelo_esperada").GetInt32());
        var candidatoSerializado = cuerpoIdentificacion.GetProperty("candidatos")[0];
        Assert.Equal(trabajadorId.ToString(), candidatoSerializado.GetProperty("trabajador_id").GetString());
        Assert.Equal("arcface-cosine-512", candidatoSerializado.GetProperty("modelo_formato").GetString());
        Assert.Equal(7, candidatoSerializado.GetProperty("version_enrolamiento").GetInt32());
        Assert.Equal(vector.Select(v => (double)v), candidatoSerializado.GetProperty("vector")
            .EnumerateArray().Select(v => v.GetDouble()));
        Assert.Equal($"Bearer {apiKeyDePrueba}", await argos.AutorizacionRecibidaAsync(
            "/api/v2/control-acceso/identificaciones"));
    }
}
