using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Requisito de entitlement (spec): el endpoint pertenece a un módulo de
// negocio y exige que el cliente del usuario lo tenga habilitado y esté
// activo. Mismo patrón que RequisitoEmailConfirmado de Caserito (Host/Auth),
// pero vive en el módulo dueño del dato.
public sealed class RequisitoModuloHabilitado : IAuthorizationRequirement
{
    public RequisitoModuloHabilitado(Modulos modulo) => Modulo = modulo;

    public Modulos Modulo { get; }
}

public sealed class ManejadorModuloHabilitado : AuthorizationHandler<RequisitoModuloHabilitado>
{
    private readonly ICurrentUser _usuario;
    private readonly IVerificadorEntitlement _entitlement;

    public ManejadorModuloHabilitado(ICurrentUser usuario, IVerificadorEntitlement entitlement)
    {
        _usuario = usuario;
        _entitlement = entitlement;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoModuloHabilitado requisito)
    {
        // Los roles de plataforma no llevan clienteId y no pasan el
        // entitlement: sus operaciones van por políticas de rol.
        if (!_usuario.EstaAutenticado || _usuario.ClienteId is not { } clienteId)
            return;

        var cancelacion = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        if (await _entitlement.TieneModuloHabilitadoAsync(clienteId, requisito.Modulo, cancelacion))
            context.Succeed(requisito);
    }
}
