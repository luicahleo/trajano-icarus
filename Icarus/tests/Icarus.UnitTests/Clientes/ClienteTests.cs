using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Domain;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class ClienteTests
{
    [Fact]
    public void CrearClienteValidoArrancaActivoYSinModulos()
    {
        var cliente = new Cliente("Granja Los Pinos S.A.C.", "20100000001");

        Assert.True(cliente.EstaActivo);
        Assert.Equal(Modulos.Ninguno, cliente.ModulosHabilitados);
        Assert.False(cliente.TieneModulo(Modulos.GestionAvicola));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RazonSocialVaciaLanzaReglaDeNegocio(string razonSocial) =>
        Assert.Throws<ReglaNegocioException>(() => new Cliente(razonSocial, "20100000001"));

    [Fact]
    public void IdentificadorFiscalVacioLanzaReglaDeNegocio() =>
        Assert.Throws<ReglaNegocioException>(() => new Cliente("Granja", " "));

    [Fact]
    public void SuspenderYReactivarCambianElEstado()
    {
        var cliente = new Cliente("Granja", "20100000001");

        cliente.Suspender();
        Assert.False(cliente.EstaActivo);
        cliente.Reactivar();
        Assert.True(cliente.EstaActivo);
    }

    [Fact]
    public void DefinirModulosAcumulaFlags()
    {
        var cliente = new Cliente("Granja", "20100000001");

        cliente.DefinirModulos(Modulos.GestionAvicola | Modulos.ControlAcceso);

        Assert.True(cliente.TieneModulo(Modulos.GestionAvicola));
        Assert.True(cliente.TieneModulo(Modulos.ControlAcceso));
    }

    [Fact]
    public void TieneModuloNingunoEsFalsoAunqueEsteDefinido()
    {
        var cliente = new Cliente("Granja", "20100000001");
        cliente.DefinirModulos(Modulos.Ninguno);

        Assert.False(cliente.TieneModulo(Modulos.Ninguno));
    }
}
