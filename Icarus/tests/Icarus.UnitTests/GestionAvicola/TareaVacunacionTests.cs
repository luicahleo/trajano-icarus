using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class TareaVacunacionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private static TareaVacunacion TareaPendiente(DateOnly? fechaProgramada = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            3, "BIO COCCIVET R", "Agua de bebida", null, fechaProgramada ?? Hoy);

    [Fact]
    public void CtorValidoNacePendienteConSnapshot()
    {
        var tarea = TareaPendiente();
        Assert.Equal(EstadoTareaVacunacion.Pendiente, tarea.Estado);
        Assert.Equal(3, tarea.EdadDia);
        Assert.Equal("BIO COCCIVET R", tarea.Vacuna);
        Assert.True(tarea.EstaActivo);
        Assert.Null(tarea.FechaAplicacion);
        Assert.Null(tarea.AvesVacunadas);
        Assert.Null(tarea.CompletadaPor);
    }

    [Fact]
    public void CtorSinVacunaLanzaReglaNegocio() =>
        Assert.Throws<ReglaNegocioException>(() =>
            new TareaVacunacion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                3, " ", null, null, Hoy));

    [Fact]
    public void CompletarConFechaFuturaLanzaReglaNegocio()
    {
        var tarea = TareaPendiente();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy.AddDays(1), null, Guid.NewGuid(), null));
        Assert.Equal("La fecha de aplicación no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CompletarConAvesCeroLanzaReglaNegocio()
    {
        var tarea = TareaPendiente();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy, 0, Guid.NewGuid(), null));
        Assert.Equal("Las aves vacunadas deben ser mayores que cero.", ex.Message);
    }

    [Fact]
    public void CompletarRegistraFechaAvesYUsuario()
    {
        var tarea = TareaPendiente();
        var usuario = Guid.NewGuid();
        tarea.Completar(Hoy.AddDays(-1), 950, usuario, "Aplicación parcial por faltante de agua.");
        Assert.Equal(EstadoTareaVacunacion.Completada, tarea.Estado);
        Assert.Equal(Hoy.AddDays(-1), tarea.FechaAplicacion);
        Assert.Equal(950, tarea.AvesVacunadas);
        Assert.Equal(usuario, tarea.CompletadaPor);
        Assert.Equal("Aplicación parcial por faltante de agua.", tarea.ObservacionesAplicacion);
    }

    [Fact]
    public void CompletarDosVecesLanzaSelladoPorEstado()
    {
        var tarea = TareaPendiente();
        tarea.Completar(Hoy, null, Guid.NewGuid(), null);
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            tarea.Completar(Hoy, null, Guid.NewGuid(), null));
        Assert.Equal("La tarea ya está cerrada.", ex.Message);
    }

    [Fact]
    public void CancelarRegistraMotivoYSella()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar("Lote con mortalidad alta, se posterga.");
        Assert.Equal(EstadoTareaVacunacion.Cancelada, tarea.Estado);
        Assert.Equal("Lote con mortalidad alta, se posterga.", tarea.MotivoCancelacion);
        Assert.Throws<ReglaNegocioException>(() => tarea.Cancelar(null));
        Assert.Throws<ReglaNegocioException>(() => tarea.Completar(Hoy, null, Guid.NewGuid(), null));
    }

    [Fact]
    public void CancelarSinMotivoQuedaSinMotivo()
    {
        var tarea = TareaPendiente();
        tarea.Cancelar(null);
        Assert.Null(tarea.MotivoCancelacion);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var tarea = TareaPendiente();
        tarea.Desactivar();
        Assert.False(tarea.EstaActivo);
    }
}
