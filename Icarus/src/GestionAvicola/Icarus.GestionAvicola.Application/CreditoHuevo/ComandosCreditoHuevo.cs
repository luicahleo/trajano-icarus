using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

public sealed record ObtenerBalanceCreditoHuevoQuery : IRequest<BalanceCreditoHuevoResumen>;

public sealed record AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

public sealed record BalanceCreditoHuevoResumen(
    decimal SaldoDisponible, IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);

public sealed class ObtenerBalanceCreditoHuevoHandler(
    IRepositorioBalanceCreditoHuevo repositorio, ICurrentUser usuarioActual)
    : IRequestHandler<ObtenerBalanceCreditoHuevoQuery, BalanceCreditoHuevoResumen>
{
    public async Task<BalanceCreditoHuevoResumen> Handle(
        ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(
            clienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        return new BalanceCreditoHuevoResumen(saldo, []);
    }
}
