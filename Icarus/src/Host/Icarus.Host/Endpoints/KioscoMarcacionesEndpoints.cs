using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using Icarus.Host.Autorizacion;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class KioscoMarcacionesEndpoints
{
    public static IEndpointRouteBuilder MapKioscoMarcaciones(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso/kiosco/marcaciones");

        grupo.MapPost("/", async Task<IResult> (
            RegistrarMarcacionRequest cuerpo, ISender mediator) =>
        {
            byte[] muestra;
            try
            {
                muestra = Convert.FromBase64String(cuerpo.MuestraBase64);
            }
            catch (FormatException)
            {
                return Results.BadRequest();
            }

            var resultado = await mediator.Send(new RegistrarMarcacionCommand(
                cuerpo.Accion, muestra, cuerpo.Formato, cuerpo.ClaveIdempotencia, cuerpo.FlujoId));
            return Results.Ok(resultado);
        })
            .RequireAuthorization(PoliticasKiosco.Autenticado)
            .AddEndpointFilter<FiltroAntiforgeryKiosco>();

        grupo.MapPost("/{propuestaId:guid}/confirmar", async (
            Guid propuestaId, ISender mediator) =>
        {
            var resultado = await mediator.Send(new ConfirmarSalidaCommand(propuestaId));
            return Results.Ok(resultado);
        })
            .RequireAuthorization(PoliticasKiosco.Autenticado)
            .AddEndpointFilter<FiltroAntiforgeryKiosco>();

        return app;
    }

    private sealed record RegistrarMarcacionRequest(
        TipoMarcacion Accion,
        string MuestraBase64,
        string Formato,
        Guid ClaveIdempotencia,
        Guid? FlujoId = null);
}
