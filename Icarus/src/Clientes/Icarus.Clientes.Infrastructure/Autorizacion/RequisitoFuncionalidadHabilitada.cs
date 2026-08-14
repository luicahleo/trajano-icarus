using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Requisito de entitlement (spec): el endpoint pertenece a una funcionalidad
// de negocio y exige que el usuario la tenga: el rol Cliente por los módulos
// de su empresa, el rol Trabajador por sus funcionalidades asignadas. La
// semántica se decide por la presencia del claim TrabajadorId en ICurrentUser
// (Clientes no conoce los nombres de rol de Identity).
public sealed class RequisitoFuncionalidadHabilitada : IAuthorizationRequirement
{
    public RequisitoFuncionalidadHabilitada(Funcionalidades funcionalidad) => Funcionalidad = funcionalidad;

    public Funcionalidades Funcionalidad { get; }
}

public sealed class ManejadorFuncionalidadHabilitada : AuthorizationHandler<RequisitoFuncionalidadHabilitada>
{
    private readonly ICurrentUser _usuario;
    private readonly IVerificadorEntitlement _entitlement;

    public ManejadorFuncionalidadHabilitada(ICurrentUser usuario, IVerificadorEntitlement entitlement)
    {
        _usuario = usuario;
        _entitlement = entitlement;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoFuncionalidadHabilitada requisito)
    {
        // Los roles de plataforma no llevan clienteId y no pasan el
        // entitlement: sus operaciones van por políticas de rol.
        if (!_usuario.EstaAutenticado || _usuario.ClienteId is not { } clienteId)
            return;

        var cancelacion = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        if (await _entitlement.TieneFuncionalidadAsync(
                clienteId, _usuario.TrabajadorId, requisito.Funcionalidad, cancelacion))
            context.Succeed(requisito);
    }
}
