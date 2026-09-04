using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8C Tarea 3 (spec: "Despacho, nota y recepción"): en Despachado, Cliente o
// Trabajador autorizado registra la cantidad realmente recibida por línea. La
// coincidencia completa contra lo despachado termina RecibidoConforme; cualquier
// diferencia termina RecibidoConDiferencias. Ambos estados son terminales: no
// hay reapertura en SP8. Los reintentos no duplican la transición.
public class RecepcionPedidoAlimentoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 4);
    private static readonly DateOnly FechaNota = new(2026, 9, 3);

    private static PedidoAlimento PedidoDespachadoDeBolsas(
        params (TipoAlimento Tipo, int Solicitada, int Entregada)[] lineas)
    {
        var pedido = new PedidoAlimento(ClienteId, ActorId,
            lineas.Select(l => new DatosDetallePedido(l.Tipo, PresentacionAlimento.Bolsa, l.Solicitada)).ToList());
        pedido.EnviarACaisy(new DateOnly(2026, 8, 28), ActorId,
            lineas.Select(l => new DatosPrecioEnvio(l.Tipo, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())).ToList());
        pedido.Aceptar(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), ActorId);
        pedido.RegistrarDespacho("NOTA-1", FechaNota, null,
            lineas.Select(l => new DatosLineaEntrega(l.Tipo, l.Entregada)).ToList(), Hoy, ActorId);
        return pedido;
    }

    [Fact]
    public void LaRecepcionSoloSeRegistraDesdeUnPedidoDespachado()
    {
        var pedido = new PedidoAlimento(ClienteId, ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);

        var excepcionBorrador = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion([new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)], ActorId));
        Assert.Equal("Solo un pedido despachado se puede recibir.", excepcionBorrador.Message);

        pedido.EnviarACaisy(new DateOnly(2026, 8, 28), ActorId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        var excepcionSolicitado = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion([new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)], ActorId));
        Assert.Equal("Solo un pedido despachado se puede recibir.", excepcionSolicitado.Message);

        pedido.Rechazar("Sin stock", ActorId);
        var excepcionRechazado = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion([new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)], ActorId));
        Assert.Equal("Solo un pedido despachado se puede recibir.", excepcionRechazado.Message);
    }

    [Fact]
    public void LaRecepcionConformeTerminaElPedidoSinDiferencias()
    {
        var pedido = PedidoDespachadoDeBolsas(
            (TipoAlimento.PosturaUno, 100, 95), (TipoAlimento.PosturaDos, 50, 50));

        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 95),
             new DatosLineaRecepcion(TipoAlimento.PosturaDos, 50)], ActorId);

        Assert.Equal(EstadoPedidoAlimento.RecibidoConforme, pedido.Estado);
        Assert.NotNull(pedido.Recepcion);
        var transicion = pedido.Historial[pedido.Historial.Count - 1];
        Assert.Equal(EstadoPedidoAlimento.Despachado, transicion.EstadoOrigen);
        Assert.Equal(EstadoPedidoAlimento.RecibidoConforme, transicion.EstadoDestino);
        // El total recibido usa la cantidad real y el precio congelado.
        Assert.Equal(95 * 180m + 50 * 180m, pedido.Recepcion!.TotalRecibido);
        Assert.Empty(pedido.Recepcion.Diferencias);
    }

    [Fact]
    public void LaRecepcionConDiferenciasCalculaElDetalleYPersisteElSnapshot()
    {
        var pedido = PedidoDespachadoDeBolsas(
            (TipoAlimento.PosturaUno, 100, 95), (TipoAlimento.PosturaDos, 50, 50));

        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 92),
             new DatosLineaRecepcion(TipoAlimento.PosturaDos, 50)], ActorId);

        Assert.Equal(EstadoPedidoAlimento.RecibidoConDiferencias, pedido.Estado);
        var diferencia = Assert.Single(pedido.Recepcion!.Diferencias);
        Assert.Equal(TipoAlimento.PosturaUno, diferencia.TipoAlimento);
        Assert.Equal(92, diferencia.CantidadRecibida);
        Assert.Equal(95, diferencia.CantidadEntregada);
        Assert.Equal(-3, diferencia.Diferencia);
        Assert.Equal(92 * 180m + 50 * 180m, pedido.Recepcion!.TotalRecibido);
    }

    [Fact]
    public void LaRecepcionDebeCubrirTodasLasLineasDelPedidoSinExtras()
    {
        var pedido = PedidoDespachadoDeBolsas(
            (TipoAlimento.PosturaUno, 100, 95), (TipoAlimento.PosturaDos, 50, 50));

        var incompleta = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion(
                [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 95)], ActorId));
        Assert.Equal("La recepción debe cubrir todas las líneas del pedido.", incompleta.Message);

        var ajena = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion(
                [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 95),
                 new DatosLineaRecepcion(TipoAlimento.PosturaDos, 50),
                 new DatosLineaRecepcion(TipoAlimento.Crecimiento, 10)], ActorId));
        Assert.Equal("La recepción incluye una línea que no pertenece al pedido.", ajena.Message);

        Assert.Equal(EstadoPedidoAlimento.Despachado, pedido.Estado);
        Assert.Null(pedido.Recepcion);
    }

    [Fact]
    public void LasCantidadesRecibidasNoPuedenSerNegativas()
    {
        var pedido = PedidoDespachadoDeBolsas((TipoAlimento.PosturaUno, 100, 95));

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion(
                [new DatosLineaRecepcion(TipoAlimento.PosturaUno, -1)], ActorId));

        Assert.Equal("La cantidad recibida no puede ser negativa.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Despachado, pedido.Estado);
    }

    [Fact]
    public void LosEstadosRecibidosSonTerminalesYElReintentoNoDuplica()
    {
        var pedido = PedidoDespachadoDeBolsas((TipoAlimento.PosturaUno, 100, 95));
        pedido.ConfirmarRecepcion([new DatosLineaRecepcion(TipoAlimento.PosturaUno, 95)], ActorId);

        var reintento = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ConfirmarRecepcion([new DatosLineaRecepcion(TipoAlimento.PosturaUno, 95)], ActorId));
        Assert.Equal("Solo un pedido despachado se puede recibir.", reintento.Message);

        // Ninguna otra transición sale de un estado recibido.
        Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-2", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 95)], Hoy, ActorId));
        Assert.Equal(EstadoPedidoAlimento.RecibidoConforme, pedido.Estado);
        Assert.Single(pedido.Recepcion!.Lineas);
        Assert.Equal(4, pedido.Historial.Count);
    }

    [Fact]
    public void LosEquivalentesRecibidosDependenDeLaPresentacion()
    {
        var pedido = new PedidoAlimento(ClienteId, ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Granel, 3),
             new DatosDetallePedido(TipoAlimento.PosturaDos, PresentacionAlimento.Granel, 3)]);
        pedido.EnviarACaisy(new DateOnly(2026, 8, 28), ActorId,
        [
            new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Granel, 175m, Guid.NewGuid()),
            new DatosPrecioEnvio(TipoAlimento.PosturaDos, PresentacionAlimento.Granel, 176m, Guid.NewGuid()),
        ]);
        pedido.Aceptar(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), ActorId);
        pedido.RegistrarDespacho("NOTA-G", FechaNota, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 3),
             new DatosLineaEntrega(TipoAlimento.PosturaDos, 2)], Hoy, ActorId);

        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 3),
             new DatosLineaRecepcion(TipoAlimento.PosturaDos, 2)], ActorId);

        Assert.Equal(EstadoPedidoAlimento.RecibidoConforme, pedido.Estado);
        var lineaDos = pedido.Recepcion!.Lineas.Single(l => l.TipoAlimento == TipoAlimento.PosturaDos);
        Assert.Equal(50, lineaDos.Equivalentes40Kg);
    }
}
