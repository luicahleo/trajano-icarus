using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
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
        servicios.AddDbContext<ClientesDbContext>((sp, opciones) =>
        {
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus"));
            opciones.AddInterceptors(
                new SaveChangesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("Clientes")),
                new TransaccionesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("Clientes")));
        });

        servicios.AddScoped<IRepositorioClientes, RepositorioClientes>();
        servicios.AddScoped<IRepositorioTrabajadores, RepositorioTrabajadores>();
        servicios.AddScoped<IClienteActivo, EstadoCliente>();
        servicios.AddScoped<IVerificadorEntitlement, VerificadorEntitlement>();
        servicios.AddScoped<IConsultaPermisosActuales, ConsultaPermisosActuales>();
        servicios.AddScoped<IAuthorizationHandler, ManejadorFuncionalidadHabilitada>();

        // IUnitOfWork resuelve al contexto de Clientes. Identity no lo consume
        // (nada lo inyecta; su registro se quitó en este plan).
        servicios.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ClientesDbContext>());

        var politicas = servicios.AddAuthorizationBuilder();
        foreach (var funcionalidad in Enum.GetValues<Funcionalidades>())
        {
            if (funcionalidad == Funcionalidades.Ninguno)
                continue;
            var politica = PoliticasClientes.Para(funcionalidad);
            politicas.AddPolicy(politica, politicaBuilder => politicaBuilder
                .RequireAuthenticatedUser()
                .AddRequirements(new RequisitoFuncionalidadHabilitada(funcionalidad)));
        }

        return servicios;
    }
}
