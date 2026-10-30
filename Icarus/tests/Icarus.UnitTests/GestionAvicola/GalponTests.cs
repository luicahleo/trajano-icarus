using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;

namespace Icarus.UnitTests.GestionAvicola;

public class GalponTests
{
    private static readonly DateOnly Ayer =
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static Galpon GalponValido() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Ayer, "Norte");

    [Fact]
    public void CtorValidoRecortaYNaceActivo()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), " A ", 5000, 4800, Ayer, "  ");
        Assert.Equal("A", galpon.Numero);
        Assert.Null(galpon.Descripcion);
        Assert.True(galpon.EstaActivo);
    }

    [Fact]
    public void CtorFechaFuturaLanzaReglaNegocio()
    {
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 0, manana, null));
        Assert.Equal("La fecha de nacimiento del lote no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CtorCapacidadCeroLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 0, 0, Ayer, null));
        Assert.Equal("La capacidad máxima debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void CtorInventarioNegativoLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, -1, Ayer, null));
        Assert.Equal("Las gallinas actuales no pueden ser negativas.", ex.Message);
    }

    [Fact]
    public void CtorInventarioSuperaCapacidadLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 5001, Ayer, null));
        Assert.Equal("Las gallinas actuales no pueden superar la capacidad máxima.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosCapacidadMenorQueInventarioLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        var ex = Assert.Throws<ReglaNegocioException>(() => galpon.ActualizarDatos("1", null, 4000, Ayer, null));
        Assert.Equal("La capacidad máxima no puede ser menor que las gallinas actuales.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosValidoRecorta()
    {
        var galpon = GalponValido();
        galpon.ActualizarDatos(" B ", " Sur ", 6000, Ayer, null);
        Assert.Equal("B", galpon.Numero);
        Assert.Equal("Sur", galpon.Descripcion);
        Assert.Equal(6000, galpon.CapacidadMaxima);
    }

    [Fact]
    public void ActualizarDatosCorrigeFechaDePoblado()
    {
        var galpon = GalponValido();
        var pobladoCorregido = Ayer.AddDays(-30);
        galpon.ActualizarDatos("1", null, 5000, pobladoCorregido, null);
        Assert.Equal(pobladoCorregido, galpon.FechaNacimientoLote);
    }

    [Fact]
    public void ActualizarDatosFechaDePobladoFuturaLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var ex = Assert.Throws<ReglaNegocioException>(() => galpon.ActualizarDatos("1", null, 5000, manana, null));
        Assert.Equal("La fecha de nacimiento del lote no puede ser futura.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosRegistraFechaDeDescarte()
    {
        var galpon = GalponValido();
        galpon.ActualizarDatos("1", null, 5000, Ayer, Ayer);
        Assert.Equal(Ayer, galpon.FechaDescarteLote);
    }

    [Fact]
    public void ActualizarDatosDescarteNuloLimpiaLaFecha()
    {
        var galpon = GalponValido();
        galpon.ActualizarDatos("1", null, 5000, Ayer, Ayer);
        galpon.ActualizarDatos("1", null, 5000, Ayer, null);
        Assert.Null(galpon.FechaDescarteLote);
    }

    [Fact]
    public void ActualizarDatosDescarteFuturoLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var ex = Assert.Throws<ReglaNegocioException>(() => galpon.ActualizarDatos("1", null, 5000, Ayer, manana));
        Assert.Equal("La fecha de descarte del lote no puede ser futura.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosDescarteAnteriorAlPobladoLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        var ex = Assert.Throws<ReglaNegocioException>(() => galpon.ActualizarDatos("1", null, 5000, Ayer, Ayer.AddDays(-1)));
        Assert.Equal("La fecha de descarte no puede ser anterior a la fecha de nacimiento del lote.", ex.Message);
    }

    [Fact]
    public void AjustarInventarioSuperaCapacidadLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        Assert.Throws<ReglaNegocioException>(() => galpon.AjustarInventarioGallinas(5001));
    }

    [Fact]
    public void AjustarInventarioValido()
    {
        var galpon = GalponValido();
        galpon.AjustarInventarioGallinas(4500);
        Assert.Equal(4500, galpon.GallinasActuales);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var galpon = GalponValido();
        galpon.Desactivar();
        Assert.False(galpon.EstaActivo);
    }
}
