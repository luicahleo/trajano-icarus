using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Lectura del catálogo global de programas de vacunación (spec SP7): la pasa
// quien tiene la funcionalidad Vacunacion (cliente por el módulo, trabajador
// por asignación) o el rol de plataforma que lo gestiona. El nombre del rol
// es contrato del JWT: Clientes no referencia Identity (regla de módulos).
public sealed class RequisitoCatalogoVacunacion : IAuthorizationRequirement
{
}

public sealed class ManejadorCatalogoVacunacion : AuthorizationHandler<RequisitoCatalogoVacunacion>
{
    private readonly ICurrentUser _usuario;
    private readonly IVerificadorEntitlement _entitlement;

    public ManejadorCatalogoVacunacion(ICurrentUser usuario, IVerificadorEntitlement entitlement)
    {
        _usuario = usuario;
        _entitlement = entitlement;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoCatalogoVacunacion requisito)
    {
        if (!_usuario.EstaAutenticado)
            return;
        if (string.Equals(_usuario.Rol, "Administrador", StringComparison.Ordinal))
        {
            context.Succeed(requisito);
            return;
        }
        if (_usuario.ClienteId is not { } clienteId)
            return;
        var cancelacion = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        if (await _entitlement.TieneFuncionalidadAsync(
                clienteId, _usuario.TrabajadorId, Funcionalidades.Vacunacion, cancelacion))
            context.Succeed(requisito);
    }
}
