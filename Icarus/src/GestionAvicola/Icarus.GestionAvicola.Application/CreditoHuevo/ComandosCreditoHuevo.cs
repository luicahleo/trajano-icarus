using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

public sealed record ObtenerBalanceCreditoHuevoQuery : IRequest<BalanceCreditoHuevoResumen>;

public sealed record AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

// RecibidoReciente y DiasReferencia acompañan al saldo, no lo modifican
// (corrección 2026-09-14). DiasReferencia viaja al cliente en vez de
// duplicar el 14 en el frontend: así el texto de la PWA no puede mentir si
// la constante cambia.
public sealed record BalanceCreditoHuevoResumen(
    decimal SaldoDisponible,
    decimal RecibidoReciente,
    int DiasReferencia,
    IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);

public sealed class ObtenerBalanceCreditoHuevoHandler(
    IRepositorioBalanceCreditoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ObtenerBalanceCreditoHuevoQuery, BalanceCreditoHuevoResumen>
{
    public async Task<BalanceCreditoHuevoResumen> Handle(
        ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        if (usuarioActual.Rol != "Cliente")
            throw new CreditoHuevoRequiereRolClienteException(
                "El crédito por despachos de huevo es exclusivo del Cliente.");
        var hoy = DespachosHuevo.FechasNegocio.Hoy();
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(clienteId, hoy, cancellationToken);
        var reciente = await repositorio.ObtenerRecibidoRecienteAsync(clienteId, hoy, cancellationToken);
        var ajustes = await repositorio.ObtenerAjustesAsync(clienteId, cancellationToken);
        return new BalanceCreditoHuevoResumen(
            saldo, reciente, ReglasCreditoHuevo.DiasReferenciaCredito, ajustes);
    }
}
