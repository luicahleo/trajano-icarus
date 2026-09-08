using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8C Tarea 1 (spec: "Despacho, nota y recepción"): CAISY registra una única
// entrega y una única nota por pedido, solo desde Aceptado. Las cantidades
// manuales van en la unidad de presentación, admiten diferencias contra lo
// solicitado y el total informado de la nota se conserva solo para contraste:
// el cálculo canónico sigue siendo el del dominio con los precios congelados.
public class EntregaPedidoAlimentoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 4);
    private static readonly DateOnly FechaNota = new(2026, 9, 3);

    private static DatosPrecioEnvio Precio(TipoAlimento tipo,
        PresentacionAlimento presentacion = PresentacionAlimento.Bolsa, decimal valor = 180m) =>
        new(tipo, presentacion, valor, Guid.NewGuid());

    private static PedidoAlimento PedidoAceptadoDeBolsas(params (TipoAlimento Tipo, int Cantidad)[] lineas) =>
        PedidoAceptado(lineas.Select(l => (l.Tipo, l.Cantidad, PresentacionAlimento.Bolsa)).ToList());

    private static PedidoAlimento PedidoAceptadoDeGranel(params (TipoAlimento Tipo, int Toneladas)[] lineas) =>
        PedidoAceptado(lineas.Select(l => (l.Tipo, l.Toneladas, PresentacionAlimento.Granel)).ToList());

    private static PedidoAlimento PedidoAceptado(
        IReadOnlyList<(TipoAlimento Tipo, int Cantidad, PresentacionAlimento Presentacion)> lineas) =>
        PedidoDesdeBorrador(lineas, b =>
        {
            b.EnviarACaisy(new DateOnly(2026, 8, 28), ActorId,
                lineas.Select(l => Precio(l.Tipo, l.Presentacion)).ToList());
            b.Aceptar(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), ActorId);
        });

    private static PedidoAlimento PedidoDesdeBorrador(
        IReadOnlyList<(TipoAlimento Tipo, int Cantidad, PresentacionAlimento Presentacion)> lineas,
        Action<PedidoAlimento> preparar)
    {
        var pedido = new PedidoAlimento(ClienteId, ActorId,
            lineas.Select(l => new DatosDetallePedido(l.Tipo, l.Presentacion, l.Cantidad)).ToList());
        preparar(pedido);
        return pedido;
    }

    private static IReadOnlyList<DatosLineaEntrega> LineasEntregadas(
        IReadOnlyList<(TipoAlimento Tipo, int Cantidad, PresentacionAlimento Presentacion)> lineas,
        int factor = 1) =>
        lineas.Select(l => new DatosLineaEntrega(l.Tipo, l.Cantidad * factor)).ToList();

    [Fact]
    public void ElDespachoSoloSeRegistraDesdeUnPedidoAceptado()
    {
        var lineas = new[] { (TipoAlimento.PosturaUno, 100, PresentacionAlimento.Bolsa) };
        var pedido = PedidoDesdeBorrador(lineas, b => { });

        var excepcionBorrador = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("N-001", FechaNota, null,
                LineasEntregadas(lineas), Hoy, ActorId));
        Assert.Equal("Solo un pedido aceptado se puede despachar.", excepcionBorrador.Message);

        pedido.EnviarACaisy(new DateOnly(2026, 8, 28), ActorId, [Precio(TipoAlimento.PosturaUno)]);
        var excepcionSolicitado = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("N-001", FechaNota, null,
                LineasEntregadas(lineas), Hoy, ActorId));
        Assert.Equal("Solo un pedido aceptado se puede despachar.", excepcionSolicitado.Message);

        pedido.Rechazar("Sin stock", ActorId);
        var excepcionRechazado = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("N-001", FechaNota, null,
                LineasEntregadas(lineas), Hoy, ActorId));
        Assert.Equal("Solo un pedido aceptado se puede despachar.", excepcionRechazado.Message);
    }

    [Fact]
    public void ElDespachoCreaUnaUnicaEntregaConSuNotaYPasaADespachado()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100));

        pedido.RegistrarDespacho("NOTA-77", FechaNota, 18000m,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId);

        Assert.Equal(EstadoPedidoAlimento.Despachado, pedido.Estado);
        Assert.NotNull(pedido.Entrega);
        Assert.Equal("NOTA-77", pedido.Entrega.NumeroNota);
        Assert.Equal(FechaNota, pedido.Entrega.FechaNota);
        Assert.Equal(Hoy, pedido.Entrega.FechaDespacho);
        Assert.Equal(18000m, pedido.Entrega.TotalNetoInformado);
        var linea = Assert.Single(pedido.Entrega.Lineas);
        Assert.Equal(TipoAlimento.PosturaUno, linea.TipoAlimento);
        Assert.Equal(100, linea.CantidadEntregada);
        var transicion = pedido.Historial[pedido.Historial.Count - 1];
        Assert.Equal(EstadoPedidoAlimento.Aceptado, transicion.EstadoOrigen);
        Assert.Equal(EstadoPedidoAlimento.Despachado, transicion.EstadoDestino);
    }

    [Fact]
    public void LaNotaExigeNumeroFechaYLineasDeTodasLasLineasDelPedido()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100), (TipoAlimento.PosturaDos, 50));

        var sinNumero = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("  ", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100),
                 new DatosLineaEntrega(TipoAlimento.PosturaDos, 50)], Hoy, ActorId));
        Assert.Equal("El número de nota es obligatorio.", sinNumero.Message);

        var sinFecha = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-1", default, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100),
                 new DatosLineaEntrega(TipoAlimento.PosturaDos, 50)], Hoy, ActorId));
        Assert.Equal("La fecha de la nota es obligatoria.", sinFecha.Message);

        var sinLineas = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-1", FechaNota, null, [], Hoy, ActorId));
        Assert.Equal("La entrega debe cubrir todas las líneas del pedido.", sinLineas.Message);

        var incompleta = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-1", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId));
        Assert.Equal("La entrega debe cubrir todas las líneas del pedido.", incompleta.Message);

        var ajena = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-1", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100),
                 new DatosLineaEntrega(TipoAlimento.PosturaDos, 50),
                 new DatosLineaEntrega(TipoAlimento.Crecimiento, 10)], Hoy, ActorId));
        Assert.Equal("La entrega incluye una línea que no pertenece al pedido.", ajena.Message);

        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
        Assert.Null(pedido.Entrega);
    }

    [Fact]
    public void LasCantidadesEntregadasNoPuedenSerNegativas()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100));

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-1", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, -1)], Hoy, ActorId));

        Assert.Equal("La cantidad entregada no puede ser negativa.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
    }

    [Fact]
    public void LaEntregaAdmiteDiferenciasContraLoSolicitado()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100));

        pedido.RegistrarDespacho("NOTA-8", FechaNota, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 90)], Hoy, ActorId);

        Assert.Equal(EstadoPedidoAlimento.Despachado, pedido.Estado);
        Assert.Equal(90, pedido.Entrega!.Lineas.Single().CantidadEntregada);
    }

    [Fact]
    public void LosEquivalentesDependenDeLaPresentacion()
    {
        var pedidoBolsas = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100));
        pedidoBolsas.RegistrarDespacho("NOTA-B", FechaNota, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 90)], Hoy, ActorId);
        Assert.Equal(90, pedidoBolsas.Entrega!.Lineas.Single().Equivalentes40Kg);

        var pedidoGranel = PedidoAceptadoDeGranel(
            (TipoAlimento.PosturaUno, 3), (TipoAlimento.PosturaDos, 3));
        pedidoGranel.RegistrarDespacho("NOTA-G", FechaNota, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 2),
             new DatosLineaEntrega(TipoAlimento.PosturaDos, 3)], Hoy, ActorId);
        Assert.Equal(50, pedidoGranel.Entrega!.Lineas.Single(l => l.TipoAlimento == TipoAlimento.PosturaUno).Equivalentes40Kg);
    }

    [Fact]
    public void ElTotalInformadoSeConservaSinSustituirElCalculoCanonico()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100), (TipoAlimento.PosturaDos, 50));
        var precioPosturaUno = pedido.Detalles.Single(d => d.TipoAlimento == TipoAlimento.PosturaUno).PrecioFinalPor40Kg!.Value;
        var precioPosturaDos = pedido.Detalles.Single(d => d.TipoAlimento == TipoAlimento.PosturaDos).PrecioFinalPor40Kg!.Value;

        pedido.RegistrarDespacho("NOTA-9", FechaNota, 99999m,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 95),
             new DatosLineaEntrega(TipoAlimento.PosturaDos, 50)], Hoy, ActorId);

        Assert.Equal(99999m, pedido.Entrega!.TotalNetoInformado);
        Assert.Equal(95 * precioPosturaUno + 50 * precioPosturaDos, pedido.TotalDespachado);
    }

    [Fact]
    public void UnSegundoDespachoDevuelveConflicto()
    {
        var pedido = PedidoAceptadoDeBolsas((TipoAlimento.PosturaUno, 100));
        pedido.RegistrarDespacho("NOTA-1", FechaNota, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.RegistrarDespacho("NOTA-2", FechaNota, null,
                [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId));

        Assert.Equal("Solo un pedido aceptado se puede despachar.", excepcion.Message);
        Assert.Equal("NOTA-1", pedido.Entrega!.NumeroNota);
        Assert.Single(pedido.Entrega.Lineas);
        Assert.Equal(3, pedido.Historial.Count);
    }
}
