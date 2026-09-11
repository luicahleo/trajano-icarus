using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// Vista de CAISY del crédito (spec 2026-09-11-credito-huevo-vista-caisy):
// el handler deriva el cliente del pedido y resuelve cuánto pesa ese pedido
// en el saldo según su estado. No verifica rol: la política del grupo
// /pedidos-alimento-caisy ya exige rol GestorCaisy más la funcionalidad
// GestorPedidoAlimento (ver ObtenerBalanceCreditoHuevoHandlerTests.cs para
// el gate del lado Cliente, que existe porque la política del tenant no
// distingue Cliente de Trabajador).
public class ObtenerCreditoHuevoDePedidoCaisyHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 1);

    private readonly IRepositorioPedidosAlimento _pedidos =
        Substitute.For<IRepositorioPedidosAlimento>();
    private readonly IRepositorioBalanceCreditoHuevo _credito =
        Substitute.For<IRepositorioBalanceCreditoHuevo>();

    private ObtenerCreditoHuevoDePedidoCaisyHandler CrearHandler() => new(_pedidos, _credito);

    private void ConSaldo(decimal saldo, params AjusteCreditoHuevoResumen[] ajustes)
    {
        _credito.ObtenerSaldoDisponibleAsync(
                ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(saldo);
        _credito.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>())
            .Returns(ajustes.ToList());
    }

    private void ConPedido(PedidoAlimento pedido) =>
        _pedidos.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

    private static PedidoAlimento PedidoBorrador() =>
        new(ClienteId, ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);

    // 100 bolsas = 100 equivalentes de 40 kg a 180 = 18 000 congelados.
    private static PedidoAlimento PedidoEnviado()
    {
        var pedido = PedidoBorrador();
        pedido.EnviarACaisy(Hoy, ActorId,
        [
            new DatosPrecioEnvio(
                TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid()),
        ]);
        return pedido;
    }

    // Recibe 80 de las 100 despachadas: 80 × 180 = 14 400. Distinto de los
    // 18 000 comprometidos a propósito, para que el test pruebe que se usó
    // Recepcion.TotalRecibido y no la suma de subtotales.
    private static PedidoAlimento PedidoRecibidoConDiferencias()
    {
        var pedido = PedidoEnviado();
        pedido.Aceptar(Hoy.AddDays(1), Hoy, ActorId);
        pedido.RegistrarDespacho("NOTA-1", Hoy, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId);
        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 80)],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512,
                "hash-sha256", "recepcion.jpg"),
            ActorId);
        return pedido;
    }

    [Fact]
    public async Task PedidoSolicitadoAportaSusSubtotalesCongelados()
    {
        var pedido = PedidoEnviado();
        ConPedido(pedido);
        ConSaldo(-5000m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(-5000m, resultado.SaldoDisponible);
        Assert.Equal(18000m, resultado.MontoDelPedido);
        Assert.Equal(13000m, resultado.SaldoSinEstePedido);
        Assert.True(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoRecibidoAportaElTotalRealmenteRecibido()
    {
        var pedido = PedidoRecibidoConDiferencias();
        ConPedido(pedido);
        ConSaldo(1000m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(14400m, resultado.MontoDelPedido);
        Assert.Equal(15400m, resultado.SaldoSinEstePedido);
        Assert.True(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoBorradorNoPesaEnElSaldo()
    {
        var pedido = PedidoBorrador();
        ConPedido(pedido);
        ConSaldo(2500m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(0m, resultado.MontoDelPedido);
        Assert.Equal(2500m, resultado.SaldoSinEstePedido);
        Assert.False(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoRechazadoNoPesaEnElSaldo()
    {
        var pedido = PedidoEnviado();
        pedido.Rechazar("Cantidad fuera de lo acordado.", ActorId);
        ConPedido(pedido);
        ConSaldo(2500m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(0m, resultado.MontoDelPedido);
        Assert.Equal(2500m, resultado.SaldoSinEstePedido);
        Assert.False(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task LosAjustesDelClienteDelPedidoViajanTalCual()
    {
        var pedido = PedidoEnviado();
        var ajuste = new AjusteCreditoHuevoResumen(
            Guid.NewGuid(), 45m, "Corrección de precio Extra.", new DateOnly(2026, 9, 1));
        ConPedido(pedido);
        ConSaldo(0m, ajuste);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        var unico = Assert.Single(resultado.Ajustes);
        Assert.Equal("Corrección de precio Extra.", unico.Motivo);
        Assert.Equal(45m, unico.Monto);
    }

    [Fact]
    public async Task PedidoInexistenteFallaSinConsultarElCredito()
    {
        var id = Guid.NewGuid();
        _pedidos.ObtenerPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(id), CancellationToken.None));

        await _credito.DidNotReceive().ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _credito.DidNotReceive().ObtenerAjustesAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
