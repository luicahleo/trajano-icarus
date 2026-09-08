using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Documentos;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboPedidoRendererQuestPdfTests
{
    private static PedidoAlimento PedidoDespachadoDeBolsas()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);
        pedido.EnviarACaisy(new DateOnly(2026, 9, 1), Guid.NewGuid(),
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        pedido.Aceptar(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 1), Guid.NewGuid());
        pedido.RegistrarDespacho("NOTA-R1", new DateOnly(2026, 9, 2), 18000m,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)],
            new DateOnly(2026, 9, 3), Guid.NewGuid());
        return pedido;
    }

    [Fact]
    public async Task GeneraUnPdfNoVacio()
    {
        var renderer = new ReciboPedidoRendererQuestPdf();

        var bytes = await renderer.RenderizarAsync(PedidoDespachadoDeBolsas(), CancellationToken.None);

        Assert.NotEmpty(bytes);
        // Firma %PDF- al inicio del archivo: confirma que es un PDF real.
        Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
    }
}
