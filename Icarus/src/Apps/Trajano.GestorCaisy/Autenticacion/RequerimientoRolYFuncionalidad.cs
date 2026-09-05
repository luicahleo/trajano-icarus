using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Trajano.GestorCaisy.Autenticacion;

// Autorización de oficina: el rol base GestorCaisy sin tenant más la
// funcionalidad CAISY concreta (combinable, no un rol). Tener el rol sin la
// función permite iniciar sesión pero no operar precios ni pedidos.
public sealed class RequerimientoRolYFuncionalidad(string rol, int bitFuncionalidad)
    : IAuthorizationRequirement
{
    public string Rol { get; } = rol;

    public int BitFuncionalidad { get; } = bitFuncionalidad;
}

public sealed class ManejadorRolYFuncionalidad : AuthorizationHandler<RequerimientoRolYFuncionalidad>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequerimientoRolYFuncionalidad requerimiento)
    {
        var conFuncion = context.User.HasClaim(c =>
                c.Type == ConstantesAutorizacion.ClaimRol
                && c.Value == requerimiento.Rol)
            && int.TryParse(
                context.User.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value,
                out var mascara)
            && (mascara & requerimiento.BitFuncionalidad) == requerimiento.BitFuncionalidad;
        if (conFuncion)
            context.Succeed(requerimiento);
        return Task.CompletedTask;
    }
}
