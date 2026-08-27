using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.GestionAvicola.Infrastructure;

public static class SemillaGestionAvicola
{
    public static readonly Guid GranjaDemoId = new("aa000000-0000-0000-0000-000000000001");
    public static readonly Guid GalponDemoNorteId = new("aa000000-0000-0000-0000-000000000011");
    public static readonly Guid GalponDemoSurId = new("aa000000-0000-0000-0000-000000000012");
    public static readonly Guid RegistroProduccionDemoAyerId = new("aa000000-0000-0000-0000-000000000031");
    public static readonly Guid RegistroProduccionDemoHoyId = new("aa000000-0000-0000-0000-000000000032");
    public static readonly Guid RegistroMortalidadDemoAyerId = new("aa000000-0000-0000-0000-000000000041");
    public static readonly Guid RegistroMortalidadDemoHoyId = new("aa000000-0000-0000-0000-000000000042");

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        if (!await db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.Id == GranjaDemoId))
        {
            db.Granjas.Add(new Granja(GranjaDemoId, clienteDemoId, "Granja Demo"));
            db.Galpones.Add(new Galpon(GalponDemoNorteId, GranjaDemoId, clienteDemoId, "1", 5000, 4800, new DateOnly(2025, 9, 1), "Galpón norte"));
            db.Galpones.Add(new Galpon(GalponDemoSurId, GranjaDemoId, clienteDemoId, "2", 5000, 5000, new DateOnly(2026, 2, 2), null));
        }
        // Registros demo del galpón norte (solo dev/test, datos ficticios):
        // uno del día anterior (sellado) y uno de hoy (día abierto, editable),
        // para agilizar las pruebas manuales de producción y mortalidad.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await db.RegistrosProduccion.IgnoreQueryFilters().AnyAsync(r => r.Id == RegistroProduccionDemoAyerId))
        {
            db.RegistrosProduccion.AddRange(
                new RegistroProduccion(RegistroProduccionDemoAyerId, GalponDemoNorteId, clienteDemoId,
                    hoy.AddDays(-1), new TimeOnly(8, 30), 96, 12, 2, 8, 4800, null),
                new RegistroProduccion(RegistroProduccionDemoHoyId, GalponDemoNorteId, clienteDemoId,
                    hoy, new TimeOnly(8, 45), 102, 5, 1, 3, 4800, null));
        }
        if (!await db.RegistrosMortalidad.IgnoreQueryFilters().AnyAsync(r => r.Id == RegistroMortalidadDemoAyerId))
        {
            db.RegistrosMortalidad.AddRange(
                new RegistroMortalidad(RegistroMortalidadDemoAyerId, GalponDemoNorteId, clienteDemoId,
                    hoy.AddDays(-1), new TimeOnly(7, 15), 4, 4800, null),
                new RegistroMortalidad(RegistroMortalidadDemoHoyId, GalponDemoNorteId, clienteDemoId,
                    hoy, new TimeOnly(7, 20), 2, 4800, null));
        }
        await db.SaveChangesAsync();
    }
}
