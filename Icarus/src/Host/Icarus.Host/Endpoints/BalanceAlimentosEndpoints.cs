using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.BalanceAlimentos;
using MediatR;

namespace Icarus.Host.Endpoints;

// Balance de alimento del tenant (spec SP8C "Balance"): solo estados
// recibidos, con la cantidad realmente recibida y el precio congelado al
// envío. El rango es obligatorio y el alcance lo impone el tenant de la
// sesión; los logs solo llevan conteos (anti-PII).
public static class BalanceAlimentosEndpoints
{
    public static IEndpointRouteBuilder MapBalanceAlimentos(this IEndpointRouteBuilder app)
    {
        var politicaTenant = PoliticasClientes.Para(Funcionalidades.PedidoAlimento);

        var grupo = app.MapGroup("/balance-alimentos").RequireAuthorization(politicaTenant);
        grupo.MapGet("/", async Task<IResult> (
            ISender mediator, DateOnly desde, DateOnly hasta,
            CancellationToken cancellationToken) =>
        {
            var balance = await mediator.Send(
                new ObtenerBalanceAlimentosQuery(desde, hasta), cancellationToken);
            return Results.Ok(balance);
        });
        return app;
    }
}
