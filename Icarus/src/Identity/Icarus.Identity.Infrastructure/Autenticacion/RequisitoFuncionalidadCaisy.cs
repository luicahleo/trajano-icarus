using System.Security.Claims;
using Icarus.Identity.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Icarus.Identity.Infrastructure.Autenticacion;

// Requisito de una funcionalidad global de CAISY (spec SP8): el token debe
// llevar el rol GestorCaisy y el bitmask con el flag encendido. Un Cliente,
// Trabajador o Administrador nunca lo cumple, aunque los demás claims
// coincidan.
public sealed class RequisitoFuncionalidadCaisy(FuncionalidadesCaisy funcionalidad)
    : IAuthorizationRequirement
{
    public FuncionalidadesCaisy Funcionalidad { get; } = funcionalidad;
}

public sealed class ManejadorFuncionalidadCaisy : AuthorizationHandler<RequisitoFuncionalidadCaisy>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequisitoFuncionalidadCaisy requirement)
    {
        if (context.User.FindFirstValue(ClaimsIdentidad.Rol) != nameof(Rol.GestorCaisy))
            return Task.CompletedTask;

        var bruto = context.User.FindFirstValue(ClaimsIdentidad.FuncionalidadesCaisy);
        if (!int.TryParse(bruto, out var bitmask) || !EsBitmaskValido(bitmask))
            return Task.CompletedTask;

        var asignadas = (FuncionalidadesCaisy)bitmask;
        if (asignadas.HasFlag(requirement.Funcionalidad))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    // El claim debe contener solo bits de funcionalidades definidas: un valor
    // desconocido o vacío no autoriza nada.
    private static bool EsBitmaskValido(int bitmask)
    {
        var todas = Enum.GetValues<FuncionalidadesCaisy>()
            .Aggregate(FuncionalidadesCaisy.Ninguno, (acumulado, f) => acumulado | f);
        var valor = (FuncionalidadesCaisy)bitmask;
        return valor is not FuncionalidadesCaisy.Ninguno
            && (valor & ~todas) == FuncionalidadesCaisy.Ninguno;
    }
}
