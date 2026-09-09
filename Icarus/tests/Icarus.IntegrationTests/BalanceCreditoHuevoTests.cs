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
// suman los despachos de huevo Recibido con recepción de hace más de 14 días
// (precio al productor congelado por cantidad) y se restan los pedidos de
// alimento realmente recibidos (RecibidoConforme / RecibidoConDiferencias).
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
    }

    [Fact]
    public async Task DespachoRecibidoHaceMenosDe14DiasNoSuma()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await SembrarAsync(DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy().AddDays(-5)));

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(0m, saldo);
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

    [Fact]
    public async Task PedidoSoloSolicitadoSinRecepcionRealRestaComoComprometidoPendiente()
    {
        // Un pedido enviado pero todavía sin recepción real ya reserva su
        // monto congelado contra el crédito (corrección de revisión: sin
        // esto, envíos concurrentes o sucesivos del mismo cliente no verían
        // el compromiso del otro y la advertencia de crédito insuficiente
        // podía perderse).
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var pedido = new PedidoAlimento(clienteId, actorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);
        pedido.EnviarACaisy(FechasNegocio.Hoy(), actorId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        await SembrarAsync(pedido);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(-18000m, saldo);
    }
}
