using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// SP9C Task 4 (spec: "Confirmar recepción y crédito"): el crédito disponible
// no se persiste como saldo, se calcula por consulta contra SQL Server: se
// suman los despachos de huevo Recibido, sin importar hace cuánto (corrección
// 2026-09-14) (precio unitario congelado por cantidad) y se restan los pedidos
// de alimento realmente recibidos (RecibidoConforme / RecibidoConDiferencias).
// Cada prueba siembra su propio tenant para no depender del orden en la base
// compartida de la colección.
[Collection(IntegracionCollection.Nombre)]
public class BalanceCreditoHuevoTests
{
    private readonly IdentityFactory _factory;

    public BalanceCreditoHuevoTests(IdentityFactory factory) => _factory = factory;

    private async Task<decimal> SaldoDeAsync(Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        return await repositorio.ObtenerSaldoDisponibleAsync(clienteId, FechasNegocio.Hoy());
    }

    private async Task<decimal> RecibidoRecienteDeAsync(Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        return await repositorio.ObtenerRecibidoRecienteAsync(clienteId, FechasNegocio.Hoy());
    }

    private async Task<IReadOnlyList<AjusteCreditoHuevoResumen>> AjustesDeAsync(Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        return await repositorio.ObtenerAjustesAsync(clienteId);
    }

    private async Task SembrarAsync(params object[] entidades)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    // 2 amarras + 30 sueltas = 390 huevos por amarra de 180 (spec SP9).
    private static DespachoHuevo DespachoRecibido(
        Guid clienteId, Guid actorId, DateOnly fechaRecepcion, decimal precio = 12.50m)
    {
        var despacho = new DespachoHuevo(clienteId, Guid.NewGuid(), actorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 2, 30)]);
        despacho.Despachar(fechaRecepcion, actorId,
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Primera, precio, Guid.NewGuid())],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "nota.jpg"));
        despacho.ConfirmarRecepcion(fechaRecepcion, actorId);
        return despacho;
    }

    private static PedidoAlimento PedidoRecibidoConforme(Guid clienteId, Guid actorId, DateOnly hoy)
    {
        var pedido = new PedidoAlimento(clienteId, actorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);
        pedido.EnviarACaisy(hoy, actorId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        pedido.Aceptar(hoy.AddDays(1), hoy, actorId);
        pedido.RegistrarDespacho("NOTA-1", hoy, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], hoy, actorId);
        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "recepcion.jpg"),
            actorId);
        return pedido;
    }

    [Fact]
    public async Task DespachoRecibidoHaceMasDe14DiasSumaAlSaldo()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await SembrarAsync(DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy().AddDays(-20)));

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(390 * 12.50m, saldo);
        // Fuera de la ventana de referencia: suma al saldo y no se señala.
        Assert.Equal(0m, await RecibidoRecienteDeAsync(clienteId));
    }

    // Corrección 2026-09-14 (segunda): el crédito nace cuando CAISY recibe el
    // huevo, no catorce días después. Los catorce días describen el ritmo con
    // que CAISY liquida, que en la práctica varía; nunca fueron condición para
    // que el dinero exista. Este test afirmaba lo contrario.
    [Fact]
    public async Task DespachoRecibidoHaceMenosDe14DiasSumaYSeReportaComoReciente()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await SembrarAsync(DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy().AddDays(-5)));

        var saldo = await SaldoDeAsync(clienteId);
        var reciente = await RecibidoRecienteDeAsync(clienteId);

        Assert.Equal(390 * 12.50m, saldo);
        // El mismo monto, en las dos cifras: el dinero cuenta Y se señala como
        // recién recibido. No son términos que se resten entre sí.
        Assert.Equal(390 * 12.50m, reciente);
    }

    [Fact]
    public async Task PedidoRecibidoConformeRestaElTotalRecibido()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var pedido = PedidoRecibidoConforme(clienteId, actorId, FechasNegocio.Hoy());
        var totalRecibido = pedido.Recepcion!.TotalRecibido;
        await SembrarAsync(pedido);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.True(totalRecibido > 0);
        Assert.Equal(-totalRecibido, saldo);
    }

    // Corrección 2026-09-14: el saldo es la cuenta real, no una proyección.
    // Un pedido enviado y todavía no recibido no consumió nada — CAISY
    // todavía puede rechazarlo o devolverlo, y un saldo que rebota hacia
    // arriba cuando eso pasa no es un saldo. El componente «comprometido
    // pendiente» existía solo para que la advertencia de crédito
    // insuficiente no se pudiera burlar con envíos sucesivos; sin
    // advertencia, no tiene razón de ser.
    [Fact]
    public async Task PedidoSolicitadoSinRecepcionRealNoDescuentaDelSaldo()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var pedido = new PedidoAlimento(clienteId, actorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);
        pedido.EnviarACaisy(FechasNegocio.Hoy(), actorId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        await SembrarAsync(pedido);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(0m, saldo);
    }

    [Fact]
    public async Task UnAjusteDeCreditoSumaAlSaldo()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        // El despacho es de hoy. Antes de la corrección del 2026-09-14 no
        // aportaba nada y el test aislaba el ajuste; ahora aporta, y el
        // esperado incluye las dos partes.
        var despacho = DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy());
        var ajuste = new AjusteCreditoHuevo(
            clienteId, despacho.Id, Guid.NewGuid(), Guid.NewGuid(), 150m, "Corrección de precio", actorId);
        await SembrarAsync(despacho, ajuste);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(390 * 12.50m + 150m, saldo);
        // El ajuste no entra en el reciente: no es huevo recibido, es una
        // corrección de un error anterior.
        Assert.Equal(390 * 12.50m, await RecibidoRecienteDeAsync(clienteId));
    }

    // Desglose visible del crédito (spec, ítem 2 del backlog): la lista de
    // ajustes es un método aparte de ObtenerSaldoDisponibleAsync — el saldo
    // sigue siendo un solo número y no se toca.
    [Fact]
    public async Task ObtenerAjustesDevuelveSoloLosDelClienteOrdenadosPorFechaDescendente()
    {
        var clienteId = Guid.NewGuid();
        var otroClienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // Dos SembrarAsync separados: CreadoEnUtc se fija en el constructor
        // del agregado (DateTime.UtcNow) y el round-trip a SQL entre ambas
        // siembras garantiza un timestamp estrictamente mayor para el
        // segundo, sin depender de la resolución del reloj del proceso.
        var masAntiguo = new AjusteCreditoHuevo(
            clienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            45m, "Corrección de precio Extra.", actorId);
        await SembrarAsync(masAntiguo);

        var masReciente = new AjusteCreditoHuevo(
            clienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            -10m, "Corrección de precio Primera.", actorId);
        var deOtroCliente = new AjusteCreditoHuevo(
            otroClienteId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            5m, "Ajeno.", actorId);
        await SembrarAsync(masReciente, deOtroCliente);

        var ajustes = await AjustesDeAsync(clienteId);

        Assert.Equal(2, ajustes.Count);
        Assert.Equal(masReciente.Id, ajustes[0].Id);
        Assert.Equal(-10m, ajustes[0].Monto);
        Assert.Equal("Corrección de precio Primera.", ajustes[0].Motivo);
        Assert.Equal(masAntiguo.Id, ajustes[1].Id);
        Assert.DoesNotContain(ajustes, a => a.Id == deOtroCliente.Id);
    }
}
