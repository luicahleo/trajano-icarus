using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Crédito por huevos despachados (spec SP9, corregido el 2026-09-14):
// disponible desde que CAISY confirma la recepción, sin espera. Sin tabla de
// saldo persistida: se calcula por consulta (suma de ingresos menos egresos
// reales más ajustes).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    // Cuánto del saldo se recibió dentro de la ventana de referencia
    // (DiasReferenciaCredito). Dato informativo que acompaña al saldo, NO un
    // término que se le reste: por construcción vale entre cero y el total de
    // ingresos. Existe porque el ritmo de liquidación de CAISY le sirve al
    // Cliente para decidir cuánto pedir, aunque no condicione nada.
    Task<decimal> ObtenerRecibidoRecienteAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    // Desglose visible del crédito: solo los ajustes de corrección, el único
    // componente que representa una sorpresa real para el cliente (spec,
    // ítem 2 del backlog). No toca ObtenerSaldoDisponibleAsync, el camino
    // crítico de SP9E.
    Task<IReadOnlyList<AjusteCreditoHuevoResumen>> ObtenerAjustesAsync(
        Guid clienteId, CancellationToken cancellationToken = default);
}

public static class ReglasCreditoHuevo
{
    // Ventana de referencia, NO un plazo de disponibilidad: el crédito existe
    // desde la recepción. Catorce días es el ritmo habitual con que CAISY
    // liquida, que en la práctica varía. Se usa solo para señalar qué parte
    // del saldo es reciente (corrección 2026-09-14; antes se llamaba
    // DiasDisponibilidadCredito y filtraba el saldo).
    public const int DiasReferenciaCredito = 14;
}

// RETIRADA por la corrección 2026-09-14: el envío de un pedido de alimento ya
// no depende del saldo, así que nadie lanza esta excepción. Se conserva, sin
// borrar, por decisión explícita del usuario. Nota: su mensaje por defecto
// llevaba voseo («Confirmá»), prohibido en este proyecto — si alguna vez se
// revive, reescribirlo en español neutro.
public sealed class CreditoInsuficienteRequiereConfirmacionException
    : ConflictException, IExcepcionConTituloPropio
{
    private const string MensajePorDefecto =
        "Este pedido dejaría el crédito del cliente en negativo. " +
        "Confirmá el envío para continuar.";

    public CreditoInsuficienteRequiereConfirmacionException() : base(MensajePorDefecto) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje) : base(mensaje) { }

    public CreditoInsuficienteRequiereConfirmacionException(string mensaje, Exception interna)
        : base(mensaje, interna) { }

    public string Titulo => "Crédito insuficiente";
}

// Regla de negocio: el crédito de huevo (saldo y ajustes) es exclusivo del
// Cliente, nunca del Trabajador — aunque tenga el entitlement de módulo
// (spec, ítem 2 del backlog). Un solo tipo para los dos puntos que la
// disparan:
// ObtenerBalanceCreditoHuevoHandler y (desde la corrección 2026-09-14)
// ObtenerCreditoHuevoDePedidoCaisyHandler.
public sealed class CreditoHuevoRequiereRolClienteException : ForbiddenException
{
    public CreditoHuevoRequiereRolClienteException() { }

    public CreditoHuevoRequiereRolClienteException(string mensaje) : base(mensaje) { }

    public CreditoHuevoRequiereRolClienteException(string mensaje, Exception interna)
        : base(mensaje, interna) { }
}
