using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.GestionAvicola.Application.BalanceAlimentos;

// Balance de alimento (spec SP8C "Balance"): solo los estados recibidos
// (RecibidoConforme y RecibidoConDiferencias) generan gasto. Cada línea suma
// los equivalentes realmente recibidos × PrecioFinalPor40Kg congelado al
// envío; el precio vigente posterior y el total informado de la nota nunca
// alteran un pedido recibido. El alcance es del tenant y el rango filtra por
// la fecha de pedido de negocio.
public interface IRepositorioBalanceAlimentos
{
    Task<IReadOnlyList<LineaBalanceAlimentos>> ObtenerAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);
}

public sealed record LineaBalanceAlimentos(
    string TipoAlimento,
    int EquivalentesRecibidos,
    int PedidosRecibidos,
    decimal Gasto);

public sealed record BalanceAlimentosResumen(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<LineaBalanceAlimentos> Lineas,
    decimal Total);

public sealed record ObtenerBalanceAlimentosQuery(DateOnly Desde, DateOnly Hasta)
    : IRequest<BalanceAlimentosResumen>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.balance.consultar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed class ObtenerBalanceAlimentosValidator
    : AbstractValidator<ObtenerBalanceAlimentosQuery>
{
    public ObtenerBalanceAlimentosValidator()
    {
        RuleFor(c => c.Desde).NotEmpty();
        RuleFor(c => c.Hasta).NotEmpty();
        RuleFor(c => c.Hasta).GreaterThanOrEqualTo(c => c.Desde)
            .WithMessage("El rango del balance es inválido.");
    }
}

public sealed class ObtenerBalanceAlimentosHandler(
    IRepositorioBalanceAlimentos repositorio,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo)
    : IRequestHandler<ObtenerBalanceAlimentosQuery, BalanceAlimentosResumen>
{
    public async Task<BalanceAlimentosResumen> Handle(
        ObtenerBalanceAlimentosQuery request, CancellationToken cancellationToken)
    {
        var clienteId = usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("Solo una cuenta de tenant consulta el balance.");
        var lineas = await repositorio.ObtenerAsync(
            clienteId, request.Desde, request.Hasta, cancellationToken);
        registroVuelo.Decidir("avicola.balance.consultar", "consulta", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = lineas.Count,
                ["EstadosRecibidos"] = 1,
            });
        return new BalanceAlimentosResumen(
            request.Desde, request.Hasta, lineas, lineas.Sum(l => l.Gasto));
    }
}
