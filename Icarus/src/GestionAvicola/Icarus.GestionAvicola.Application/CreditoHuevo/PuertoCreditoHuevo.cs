using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Crédito por huevos despachados (spec SP9): disponible recién catorce días
// después de la recepción. Sin tabla de saldo persistida: se calcula por
// consulta (suma de ingresos disponibles menos egresos reales).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);
}

public static class ReglasCreditoHuevo
{
    public const int DiasDisponibilidadCredito = 14;
}

// Exige confirmación explícita del cliente antes de enviar un pedido que
// dejaría su crédito por despachos de huevo en negativo (spec SP9E). Hereda
// de ConflictException (409): la decisión final de aceptar el pedido sigue
// siendo de CAISY al aceptar/rechazar, así que esto no es un bloqueo duro
// sin salida — solo exige el paso consciente de confirmar.
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
