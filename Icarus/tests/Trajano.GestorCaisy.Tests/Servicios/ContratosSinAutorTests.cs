using System.Reflection;
using Trajano.GestorCaisy.Servicios;
using Xunit;

namespace Trajano.GestorCaisy.Tests.Servicios;

// Decisión central de la spec 2026-09-14: GestionAvicola guarda el id del
// autor, pero CAISY nunca ve personas: ve el folio. Este test vuelve ejecutable
// la decisión: ningún DTO que CAISY consume expone un autor.
public class ContratosSinAutorTests
{
    private static readonly string[] PalabrasProhibidas = ["Autor", "Trabajador", "CreadoPor"];

    [Fact]
    public void LosContratosDeCaisyNoExponenAutores()
    {
        var tipos = new[]
        {
            typeof(PedidoResumenApi),
            typeof(PedidoDetalleApi),
            typeof(LineaPedidoApi),
            typeof(TransicionPedidoApi),
            typeof(EntregaPedidoApi),
            typeof(RecepcionPedidoApi),
            typeof(DespachoHuevoResumenApi),
            typeof(DespachoHuevoDetalleApi),
            typeof(DetalleDespachoHuevoApi),
        };

        foreach (var tipo in tipos)
        {
            foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(
                    PalabrasProhibidas,
                    palabra => propiedad.Name.Contains(palabra, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
