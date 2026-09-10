using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9D (spec: "Corrección de una publicación vigente"): registro inmutable
// de la diferencia de crédito entre el precio erróneo y el correcto para un
// despacho ya Recibido.
public class AjusteCreditoHuevoTests
{
    [Fact]
    public void SeCreaConLosDatosDeLaCorreccion()
    {
        var clienteId = Guid.NewGuid();
        var despachoId = Guid.NewGuid();
        var erroneaId = Guid.NewGuid();
        var correctivaId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var ajuste = new AjusteCreditoHuevo(
            clienteId, despachoId, erroneaId, correctivaId, 125.50m, "Precio mal digitado.", actorId);

        Assert.Equal(clienteId, ajuste.ClienteId);
        Assert.Equal(despachoId, ajuste.DespachoHuevoId);
        Assert.Equal(erroneaId, ajuste.PublicacionErroneaId);
        Assert.Equal(correctivaId, ajuste.PublicacionCorrectivaId);
        Assert.Equal(125.50m, ajuste.Monto);
        Assert.Equal("Precio mal digitado.", ajuste.Motivo);
        Assert.Equal(actorId, ajuste.ActorId);
    }

    [Fact]
    public void RechazaUnMontoCero()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new AjusteCreditoHuevo(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, "motivo", Guid.NewGuid()));

        Assert.Equal("El monto del ajuste no puede ser cero.", excepcion.Message);
    }

    [Fact]
    public void RechazaUnMotivoVacio()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new AjusteCreditoHuevo(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "  ", Guid.NewGuid()));

        Assert.Equal("El ajuste debe declarar un motivo.", excepcion.Message);
    }
}
