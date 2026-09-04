using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Application.Vacunacion;
using Icarus.GestionAvicola.Infrastructure.Documentos;
using Icarus.GestionAvicola.Infrastructure.Importacion;
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
        servicios.AddScoped<IRepositorioProduccion, RepositorioProduccion>();
        servicios.AddScoped<IRepositorioMortalidad, RepositorioMortalidad>();
        servicios.AddScoped<IRepositorioProgramasVacunacion, RepositorioProgramasVacunacion>();
        servicios.AddScoped<IRepositorioTareasVacunacion, RepositorioTareasVacunacion>();
        servicios.AddScoped<IImportadorCronogramaVacunacion, ImportadorCronogramaVacunacion>();
        servicios.AddScoped<IRepositorioNotificacionesPrecios, RepositorioNotificacionesPrecios>();
        servicios.AddScoped<IImportadorNotificacionPreciosPdf, ImportadorNotificacionPreciosPdf>();
        servicios.AddScoped<IAlmacenDocumentosPrecios, AlmacenDocumentosLocal>();
        servicios.AddScoped<IUnidadTrabajoGestionAvicola>(sp =>
            new UnidadTrabajoConConcurrencia(sp.GetRequiredService<GestionAvicolaDbContext>()));
        return servicios;
    }
}
