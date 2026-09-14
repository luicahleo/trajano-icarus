using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// Trazabilidad de granja y autor (spec 2026-09-14). El folio lo genera una
// SEQUENCE de SQL Server, así que solo se puede comprobar contra una base
// real: un test unitario vería siempre cero.
[Collection(IntegracionCollection.Nombre)]
public class TrazabilidadRegistrosTests
{
    private readonly IdentityFactory _factory;

    public TrazabilidadRegistrosTests(IdentityFactory factory) => _factory = factory;

    private async Task SembrarAsync(params object[] entidades)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UnPedidoGuardaGranjaAutorYRecibeFolioCorrelativo()
    {
        var clienteId = Guid.NewGuid();
        var granjaId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();

        var primero = new PedidoAlimento(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 10)]);
        var segundo = new PedidoAlimento(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 5)]);
        // Dos SaveChanges y no uno: EF inserta en lote con MERGE ... OUTPUT y
        // SQL Server no garantiza que los valores generados vuelvan en el
        // orden de las entidades. En producción cada pedido es su propia
        // transacción, así que sembrar por separado refleja el caso real.
        await SembrarAsync(primero);
        await SembrarAsync(segundo);

        Assert.Equal(granjaId, primero.GranjaId);
        Assert.Equal(trabajadorId, primero.CreadoPorTrabajadorId);
        // Correlativo, no necesariamente consecutivo: una SEQUENCE puede dejar
        // huecos si una transacción se revierte. Lo que importa es el orden.
        Assert.True(primero.Numero > 0, $"El primero quedó con folio {primero.Numero}.");
        Assert.True(segundo.Numero > primero.Numero,
            $"El segundo ({segundo.Numero}) debía superar al primero ({primero.Numero}).");
    }

    [Fact]
    public async Task UnPedidoCreadoPorElClienteNoTieneTrabajador()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 10)]);
        await SembrarAsync(pedido);

        Assert.Null(pedido.CreadoPorTrabajadorId);
    }

    [Fact]
    public async Task UnDespachoGuardaAutorYRecibeFolioDeSuPropiaSerie()
    {
        // Series independientes: el primer despacho de la base no hereda el
        // contador de los pedidos.
        var clienteId = Guid.NewGuid();
        var granjaId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();

        var primero = new DespachoHuevo(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 1, 0)]);
        var segundo = new DespachoHuevo(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 1, 0)]);
        await SembrarAsync(primero);
        await SembrarAsync(segundo);

        Assert.Equal(trabajadorId, primero.CreadoPorTrabajadorId);
        Assert.True(primero.Numero > 0, $"El primero quedó con folio {primero.Numero}.");
        Assert.True(segundo.Numero > primero.Numero,
            $"El segundo ({segundo.Numero}) debía superar al primero ({primero.Numero}).");
    }
}
