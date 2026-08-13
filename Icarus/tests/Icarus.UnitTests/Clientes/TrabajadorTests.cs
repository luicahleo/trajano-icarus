using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Domain;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class TrabajadorTests
{
    private static readonly DateOnly IngresoValido = new(2026, 1, 15);

    private static Trabajador Crear() =>
        new(Guid.NewGuid(), "Nombre Ficticio", "00000000", "Operario", IngresoValido);

    [Fact]
    public void TrabajadorValidoArrancaActivoSinCese()
    {
        var trabajador = Crear();

        Assert.True(trabajador.EstaActivo);
        Assert.Null(trabajador.FechaCese);
    }

    [Fact]
    public void ClienteIdVacioLanzaReglaDeNegocio() =>
        Assert.Throws<ReglaNegocioException>(() =>
            new Trabajador(Guid.Empty, "Nombre", "00000000", "Operario", IngresoValido));

    [Fact]
    public void FechaIngresoFuturaLanzaReglaDeNegocio() =>
        Assert.Throws<ReglaNegocioException>(() =>
            new Trabajador(Guid.NewGuid(), "Nombre", "00000000", "Operario",
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));

    [Fact]
    public void CeseFuturoLanzaReglaDeNegocio()
    {
        var trabajador = Crear();

        Assert.Throws<ReglaNegocioException>(() =>
            trabajador.Cesar(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));
    }

    [Fact]
    public void CeseAnteriorAlIngresoLanzaReglaDeNegocio()
    {
        var trabajador = Crear();

        Assert.Throws<ReglaNegocioException>(() => trabajador.Cesar(IngresoValido.AddDays(-1)));
    }

    [Fact]
    public void CeseValidoQuedaRegistrado()
    {
        var trabajador = Crear();

        trabajador.Cesar(IngresoValido.AddDays(10));

        Assert.Equal(IngresoValido.AddDays(10), trabajador.FechaCese);
    }

    [Fact]
    public void DesactivarMarcaSoftDelete()
    {
        var trabajador = Crear();

        trabajador.Desactivar();

        Assert.False(trabajador.EstaActivo);
    }
}
