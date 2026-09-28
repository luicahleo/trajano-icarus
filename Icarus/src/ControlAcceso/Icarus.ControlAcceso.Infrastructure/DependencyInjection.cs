using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Infrastructure.Biometria;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.ControlAcceso.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.ControlAcceso.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddControlAccesoInfrastructure(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContext<ControlAccesoDbContext>((sp, opciones) =>
        {
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus"));
            opciones.AddInterceptors(
                new SaveChangesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("ControlAcceso")),
                new TransaccionesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("ControlAcceso")));
        });

        servicios.AddScoped<IRepositorioJornadasAcceso, RepositorioJornadasAcceso>();
        servicios.AddScoped<IRepositorioPlantillasFaciales, RepositorioPlantillasFaciales>();
        servicios.AddScoped<IUnidadTrabajoControlAcceso>(sp =>
            new UnidadTrabajoControlAccesoConConcurrencia(sp.GetRequiredService<ControlAccesoDbContext>()));

        servicios.AddOptions<OpcionesProteccionPlantillas>()
            .Bind(configuracion.GetSection(OpcionesProteccionPlantillas.Seccion));
        servicios.AddSingleton<ProtectorPlantillas>();

        return servicios;
    }
}
