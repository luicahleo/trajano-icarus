using Icarus.Host.Autorizacion;

namespace Icarus.Host.Endpoints;

public static class ControlAccesoEndpoints
{
    // Sondeo del mecanismo de autorización de ControlAcceso: se mapea solo en
    // Development y Testing mientras no haya endpoints de negocio (spec).
    public static IEndpointRouteBuilder MapControlAccesoSondeo(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/control-acceso/sondeo");

        grupo.MapGet("/administrar", () => Results.Ok(new { estado = "ok" }))
            .RequireAuthorization(PoliticasControlAcceso.ClienteConControlAcceso);
        grupo.MapGet("/marcar", () => Results.Ok(new { estado = "ok" }))
            .RequireAuthorization(PoliticasControlAcceso.TrabajadorElegibleParaMarcar);

        return app;
    }
}
