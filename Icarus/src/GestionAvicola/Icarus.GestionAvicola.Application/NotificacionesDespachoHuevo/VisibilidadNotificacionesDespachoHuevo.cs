using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;

// Visibilidad de la bandeja de notificaciones del despacho de huevo por rol
// (spec SP9F).
//
// La regla se expresa como EXCLUSIÓN del rol Trabajador y no como inclusión
// del rol Cliente, a propósito: DespachosHuevoEndpoints mapea los mismos dos
// endpoints en el grupo tenant y en el grupo caisy, así que los dos alcances
// comparten handler. Un gate positivo de «solo Cliente» —como el de
// ObtenerBalanceCreditoHuevoHandler, que sí es exclusivo del tenant— le
// sacaría a GestorCaisy y a Administrador sus notificaciones
// CreditoInsuficiente y rompería la bandeja ya desplegada de
// Trajano.GestorCaisy.
//
// El rol viaja como string y se compara con un literal: este proyecto solo
// referencia su propio Domain y BuildingBlocks.Application, y la prueba
// ReglasDeModulosTests.GestionAvicolaNoSeReferenciaConOtrosModulos impide
// referenciar Icarus.Identity.Domain para usar el enum Rol.
public static class VisibilidadNotificacionesDespachoHuevo
{
    public const string RolTrabajador = "Trabajador";

    private static readonly TipoNotificacionDespachoHuevo[] Todos =
        Enum.GetValues<TipoNotificacionDespachoHuevo>();

    private static readonly TipoNotificacionDespachoHuevo[] SinFinancieros =
        [.. Todos.Where(tipo => !EsFinanciero(tipo))];

    public static IReadOnlyCollection<TipoNotificacionDespachoHuevo> Para(string? rol) =>
        string.Equals(rol, RolTrabajador, StringComparison.Ordinal) ? SinFinancieros : Todos;

    // Financiero = lleva plata o el id de un pedido en Meta. El Trabajador
    // nunca lo ve, aunque tenga el entitlement del módulo: misma regla de
    // negocio que CreditoHuevoRequiereRolClienteException.
    //
    // La rama por defecto es fail-closed a propósito: un tipo nuevo sin
    // clasificar se trata como financiero y queda oculto al Trabajador en vez
    // de filtrársele. CadaTipoDelEnumEstaClasificadoExplicitamente avisa en
    // rojo cuando eso pasa.
    private static bool EsFinanciero(TipoNotificacionDespachoHuevo tipo) => tipo switch
    {
        TipoNotificacionDespachoHuevo.DespachoRecibido => false,
        TipoNotificacionDespachoHuevo.CreditoInsuficiente => true,
        TipoNotificacionDespachoHuevo.AjusteCredito => true,
        _ => true,
    };
}
