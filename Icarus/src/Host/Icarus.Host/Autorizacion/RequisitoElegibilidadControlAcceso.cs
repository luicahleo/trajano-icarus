using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Host.Autorizacion;

public sealed class RequisitoClienteConControlAcceso : IAuthorizationRequirement
{
}

public sealed class RequisitoTrabajadorElegibleParaMarcar : IAuthorizationRequirement
{
}

// Handler para administración del módulo: rol Cliente sobre su propio tenant
// con el módulo habilitado. La decisión final la toma el puerto de
// ControlAcceso; el handler solo traduce claims -> consulta.
public sealed class ManejadorClienteConControlAcceso :
    AuthorizationHandler<RequisitoClienteConControlAcceso>
{
    private readonly ICurrentUser _usuario;
    private readonly IConsultaElegibilidadAcceso _consulta;

    public ManejadorClienteConControlAcceso(
        ICurrentUser usuario, IConsultaElegibilidadAcceso consulta)
    {
        _usuario = usuario;
        _consulta = consulta;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoClienteConControlAcceso requisito)
    {
        if (!_usuario.EstaAutenticado || _usuario.ClienteId is not { } clienteId)
            return;
        if (!string.Equals(_usuario.Rol, nameof(Rol.Cliente), StringComparison.Ordinal))
            return;

        var elegibilidad = await _consulta.EvaluarAsync(
            clienteId, _usuario.TrabajadorId, Cancelacion(context));
        if (elegibilidad.PuedeAdministrar)
            context.Succeed(requisito);
    }

    private static CancellationToken Cancelacion(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext http)
            return http.RequestAborted;
        return CancellationToken.None;
    }
}

// Handler para marcación: trabajador del tenant, activo, sin cese y con el
// módulo habilitado. No exige funcionalidades de GestionAvicola.
public sealed class ManejadorTrabajadorElegibleParaMarcar :
    AuthorizationHandler<RequisitoTrabajadorElegibleParaMarcar>
{
    private readonly ICurrentUser _usuario;
    private readonly IConsultaElegibilidadAcceso _consulta;

    public ManejadorTrabajadorElegibleParaMarcar(
        ICurrentUser usuario, IConsultaElegibilidadAcceso consulta)
    {
        _usuario = usuario;
        _consulta = consulta;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoTrabajadorElegibleParaMarcar requisito)
    {
        if (!_usuario.EstaAutenticado || _usuario.ClienteId is not { } clienteId
            || _usuario.TrabajadorId is not { } trabajadorId)
            return;

        var elegibilidad = await _consulta.EvaluarAsync(
            clienteId, trabajadorId, Cancelacion(context));
        if (elegibilidad.PuedeMarcar)
            context.Succeed(requisito);
    }

    private static CancellationToken Cancelacion(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext http)
            return http.RequestAborted;
        return CancellationToken.None;
    }
}
