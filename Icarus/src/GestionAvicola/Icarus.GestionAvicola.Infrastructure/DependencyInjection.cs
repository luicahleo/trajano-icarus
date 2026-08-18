using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.GestionAvicola.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.GestionAvicola.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGestionAvicolaInfraestructura(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContext<GestionAvicolaDbContext>((sp, opciones) =>
        {
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus"));
            opciones.AddInterceptors(
                new SaveChangesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(), new DescriptorContextoPersistencia("GestionAvicola")),
                new TransaccionesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(), new DescriptorContextoPersistencia("GestionAvicola")));
        });
        servicios.AddScoped<IRepositorioGranjas, RepositorioGranjas>();
        servicios.AddScoped<IRepositorioGalpones, RepositorioGalpones>();
        servicios.AddScoped<IUnidadTrabajoGestionAvicola>(sp => sp.GetRequiredService<GestionAvicolaDbContext>());
        return servicios;
    }
}
