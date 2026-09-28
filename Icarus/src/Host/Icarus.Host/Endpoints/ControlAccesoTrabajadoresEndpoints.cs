using Icarus.ControlAcceso.Application.Trabajadores;
using Icarus.Host.Autorizacion;
using MediatR;

namespace Icarus.Host.Endpoints;

public static class ControlAccesoTrabajadoresEndpoints
{
    public static IEndpointRouteBuilder MapControlAccesoTrabajadores(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso/trabajadores");

        grupo.MapGet("/acceso", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarAccesoTrabajadoresQuery())))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPut("/{trabajadorId:guid}/habilitacion", async (
            Guid trabajadorId, HabilitacionRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new DefinirHabilitacionCommand(trabajadorId, cuerpo.Habilitado));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/{trabajadorId:guid}/enrolamiento", async Task<IResult> (
            Guid trabajadorId, EnrolamientoRequest cuerpo, ISender mediator) =>
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

            var resultado = await mediator.Send(new EnrolarTrabajadorCommand(
                trabajadorId, muestra, cuerpo.Formato, cuerpo.ClaveIdempotencia));
            // Fallo de PAD o identificación: respuesta genérica, sin motivo
            // biométrico ni identidades.
            return Results.Ok(new
            {
                resultado.Exitoso,
                resultado.VersionEnrolamiento,
                resultado.Motivo,
            });
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        grupo.MapPost("/{trabajadorId:guid}/revocacion", async (Guid trabajadorId, ISender mediator) =>
        {
            await mediator.Send(new RevocarRostroCommand(trabajadorId));
            return Results.NoContent();
        }).RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);

        return app;
    }

    private sealed record HabilitacionRequest(bool Habilitado);

    private sealed record EnrolamientoRequest(
        string MuestraBase64, string Formato, Guid ClaveIdempotencia);
}
