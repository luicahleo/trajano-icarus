using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Trajano.GestorCaisy.Autenticacion;

namespace Trajano.GestorCaisy.Tests.Autenticacion;

public class ManejadorFuncionalidadTests
{
    [Fact]
    public async Task GestorConLaFuncionalidadSatisfaceElRequerimiento()
    {
        var contexto = ContextoDe(Usuario("GestorCaisy", 1));

        await new ManejadorRolYFuncionalidad().HandleAsync(contexto);

        Assert.True(contexto.HasSucceeded);
    }

    [Fact]
    public async Task FuncionalidadCombinadaSatisfaceElRequerimiento()
    {
        // FuncionalidadesCaisy es un bitmask componible: dos funciones
        // encendidas siguen concediendo la de pedidos de alimento.
        var contexto = ContextoDe(Usuario("GestorCaisy", 3));

        await new ManejadorRolYFuncionalidad().HandleAsync(contexto);

        Assert.True(contexto.HasSucceeded);
    }

    [Fact]
    public async Task GestorSinLaFuncionalidadNoSatisfaceElRequerimiento()
    {
        var contexto = ContextoDe(Usuario("GestorCaisy", 0));

        await new ManejadorRolYFuncionalidad().HandleAsync(contexto);

        Assert.False(contexto.HasSucceeded);
    }

    [Fact]
    public async Task RolAjenoNoSatisfaceElRequerimiento()
    {
        var contexto = ContextoDe(Usuario("Cliente", 1));

        await new ManejadorRolYFuncionalidad().HandleAsync(contexto);

        Assert.False(contexto.HasSucceeded);
    }

    [Fact]
    public async Task SinElClaimDeFuncionalidadesNoSatisfaceElRequerimiento()
    {
        var contexto = ContextoDe(Usuario("GestorCaisy", null));

        await new ManejadorRolYFuncionalidad().HandleAsync(contexto);

        Assert.False(contexto.HasSucceeded);
    }

    private static AuthorizationHandlerContext ContextoDe(ClaimsPrincipal usuario) =>
        new([new RequerimientoRolYFuncionalidad(
            ConstantesAutorizacion.RolGestorCaisy,
            ConstantesAutorizacion.BitGestorPedidoAlimento)], usuario, null);

    private static ClaimsPrincipal Usuario(string rol, int? funcCaisy)
    {
        var claims = new List<Claim> { new("rol", rol) };
        if (funcCaisy is { } valor)
            claims.Add(new Claim("funcCaisy", valor.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba"));
    }
}
