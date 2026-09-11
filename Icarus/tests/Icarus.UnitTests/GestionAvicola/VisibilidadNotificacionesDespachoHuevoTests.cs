using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9F: la bandeja de notificaciones del despacho de huevo esconde los tipos
// financieros al rol Trabajador y no cambia nada para los demás roles. La
// regla es una EXCLUSIÓN del Trabajador, no una inclusión del Cliente: el
// mismo handler sirve al grupo tenant y al grupo caisy, así que un gate
// positivo de «solo Cliente» le sacaría a GestorCaisy sus notificaciones.
public class VisibilidadNotificacionesDespachoHuevoTests
{
    [Fact]
    public void ElTrabajadorNoVeLosTiposFinancieros()
    {
        var visibles = VisibilidadNotificacionesDespachoHuevo.Para("Trabajador");

        Assert.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido, visibles);
        Assert.DoesNotContain(TipoNotificacionDespachoHuevo.AjusteCredito, visibles);
        Assert.DoesNotContain(TipoNotificacionDespachoHuevo.CreditoInsuficiente, visibles);
    }

    [Theory]
    [InlineData("Cliente")]
    [InlineData("GestorCaisy")]
    [InlineData("Administrador")]
    [InlineData(null)]
    public void LosDemasRolesVenTodosLosTipos(string? rol)
    {
        var visibles = VisibilidadNotificacionesDespachoHuevo.Para(rol);

        Assert.Equal(Enum.GetValues<TipoNotificacionDespachoHuevo>().Length, visibles.Count);
        Assert.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido, visibles);
        Assert.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente, visibles);
        Assert.Contains(TipoNotificacionDespachoHuevo.AjusteCredito, visibles);
    }

    // Red de seguridad: si alguien agrega un valor al enum y no lo clasifica,
    // esta prueba falla y lo obliga a decidir si es financiero. La rama por
    // defecto de la clasificación es fail-closed (financiero), así que un
    // olvido esconde el tipo nuevo al Trabajador en vez de filtrárselo.
    [Fact]
    public void CadaTipoDelEnumEstaClasificadoExplicitamente()
    {
        var esperados = new Dictionary<TipoNotificacionDespachoHuevo, bool>
        {
            [TipoNotificacionDespachoHuevo.DespachoRecibido] = false,
            [TipoNotificacionDespachoHuevo.CreditoInsuficiente] = true,
            [TipoNotificacionDespachoHuevo.AjusteCredito] = true,
        };

        Assert.Equal(
            Enum.GetValues<TipoNotificacionDespachoHuevo>().OrderBy(t => t).ToArray(),
            esperados.Keys.OrderBy(t => t).ToArray());

        var visiblesTrabajador = VisibilidadNotificacionesDespachoHuevo.Para(
            VisibilidadNotificacionesDespachoHuevo.RolTrabajador);
        foreach (var (tipo, esFinanciero) in esperados)
            Assert.Equal(!esFinanciero, visiblesTrabajador.Contains(tipo));
    }
}
