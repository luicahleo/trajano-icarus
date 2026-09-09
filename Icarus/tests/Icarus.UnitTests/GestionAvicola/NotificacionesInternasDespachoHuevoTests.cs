using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class NotificacionesInternasDespachoHuevoTests
{
    [Fact]
    public void ParaRecepcionConfirmadaLlevaClienteYDespacho()
    {
        var despachoId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(despachoId, clienteId);

        Assert.Equal(TipoNotificacionDespachoHuevo.DespachoRecibido, notificacion.Tipo);
        Assert.Equal(despachoId, notificacion.DespachoHuevoId);
        Assert.Equal(clienteId, notificacion.ClienteId);
        Assert.False(notificacion.Leida);
    }

    [Fact]
    public void ParaCreditoInsuficienteEsGlobalSinDespacho()
    {
        var pedidoId = Guid.NewGuid();

        var notificacion = NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente(pedidoId);

        Assert.Equal(TipoNotificacionDespachoHuevo.CreditoInsuficiente, notificacion.Tipo);
        Assert.Null(notificacion.DespachoHuevoId);
        Assert.Null(notificacion.ClienteId);
        Assert.Equal(pedidoId.ToString(), notificacion.Meta);
    }

    [Fact]
    public void MarcarLeidaEsIdempotente()
    {
        var actorId = Guid.NewGuid();
        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(Guid.NewGuid(), Guid.NewGuid());

        notificacion.MarcarLeida(actorId);
        var primeraFecha = notificacion.FechaLeidaUtc;
        notificacion.MarcarLeida(Guid.NewGuid());

        Assert.True(notificacion.Leida);
        Assert.Equal(actorId, notificacion.LeidaPor);
        Assert.Equal(primeraFecha, notificacion.FechaLeidaUtc);
    }
}
