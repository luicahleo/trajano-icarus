using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Lectura del catálogo global de programas de vacunación (spec SP7 y spec
// 2026-10-30-gestor-veterinario-vacunacion): la pasa solo quien tiene la
// funcionalidad Vacunacion (cliente por el módulo, trabajador por
// asignación). El Administrador de plataforma ya no la pasa: el catálogo lo
// gestiona el Gestor CAISY por sus propias rutas.
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
        if (!_usuario.EstaAutenticado || _usuario.ClienteId is not { } clienteId)
            return;
        var cancelacion = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        if (await _entitlement.TieneFuncionalidadAsync(
                clienteId, _usuario.TrabajadorId, Funcionalidades.Vacunacion, cancelacion))
            context.Succeed(requisito);
    }
}
