using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.Clientes.Infrastructure.Persistencia;
using Icarus.Clientes.Infrastructure.Repositorios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.Clientes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClientesInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContext<ClientesDbContext>(opciones =>
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus")));

        servicios.AddScoped<IRepositorioClientes, RepositorioClientes>();
        servicios.AddScoped<IRepositorioTrabajadores, RepositorioTrabajadores>();
        servicios.AddScoped<IVerificadorEntitlement, VerificadorEntitlement>();
        servicios.AddScoped<IAuthorizationHandler, ManejadorModuloHabilitado>();

        // IUnitOfWork resuelve al contexto de Clientes. Identity no lo consume
        // (nada lo inyecta; su registro se quitó en este plan).
        servicios.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ClientesDbContext>());

        servicios.AddAuthorizationBuilder()
            .AddPolicy(PoliticasClientes.RequiereGestionAvicola, politica => politica
                .RequireAuthenticatedUser()
                .AddRequirements(new RequisitoModuloHabilitado(Modulos.GestionAvicola)))
            .AddPolicy(PoliticasClientes.RequiereControlAcceso, politica => politica
                .RequireAuthenticatedUser()
                .AddRequirements(new RequisitoModuloHabilitado(Modulos.ControlAcceso)));

        return servicios;
    }
}
