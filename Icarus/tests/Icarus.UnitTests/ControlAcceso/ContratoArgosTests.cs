using System.Text;
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
