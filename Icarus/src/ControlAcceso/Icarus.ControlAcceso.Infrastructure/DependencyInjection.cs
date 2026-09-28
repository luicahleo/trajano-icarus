using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Application.Trabajadores;
using Icarus.ControlAcceso.Infrastructure.Argos;
using Icarus.ControlAcceso.Infrastructure.Biometria;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Icarus.ControlAcceso.Infrastructure.Repositorios;
using Icarus.ControlAcceso.Infrastructure.Tiempo;
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
        servicios.AddScoped<IRepositorioSesionesKiosco, RepositorioSesionesKiosco>();
        servicios.AddScoped<IRepositorioAccesoTrabajadores, RepositorioOperacionesEnrolamiento>();
        servicios.AddScoped<IRepositorioOperacionesMarcacion, RepositorioOperacionesMarcacion>();
        servicios.AddScoped<IUnidadTrabajoControlAcceso>(sp =>
            new UnidadTrabajoControlAccesoConConcurrencia(sp.GetRequiredService<ControlAccesoDbContext>()));

        servicios.AddSingleton(TimeProvider.System);
        servicios.AddScoped<IRelojAcceso, RelojBolivia>();

        servicios.AddOptions<OpcionesProteccionPlantillas>()
            .Bind(configuracion.GetSection(OpcionesProteccionPlantillas.Seccion));
        servicios.AddSingleton<ProtectorPlantillas>();
        servicios.AddSingleton<IProtectorPlantillas>(sp => sp.GetRequiredService<ProtectorPlantillas>());

        servicios.AddOptions<OpcionesKiosco>()
            .Bind(configuracion.GetSection(OpcionesKiosco.Seccion));

        servicios.AddOptions<OpcionesBiometria>()
            .Bind(configuracion.GetSection(OpcionesBiometria.Seccion));
        var biometria = configuracion.GetSection(OpcionesBiometria.Seccion).Get<OpcionesBiometria>()
            ?? new OpcionesBiometria();
        if (biometria.UsarDoble)
            servicios.AddSingleton<IProveedorIdentidadFacial, ProveedorIdentidadFacialDoble>();
        else
            servicios.AddSingleton<IProveedorIdentidadFacial, ProveedorIdentidadFacialNoDisponible>();

        return servicios;
    }
}
