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
    public static readonly Guid ProgramaVacunacionDemoId = new("aa000000-0000-0000-0000-000000000021");

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        if (!await db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.Id == GranjaDemoId))
        {
            db.Granjas.Add(new Granja(GranjaDemoId, clienteDemoId, "Granja Demo"));
            db.Galpones.Add(new Galpon(GalponDemoNorteId, GranjaDemoId, clienteDemoId, "1", 5000, 4800, new DateOnly(2025, 9, 1), "Galpón norte"));
            db.Galpones.Add(new Galpon(GalponDemoSurId, GranjaDemoId, clienteDemoId, "2", 5000, 5000, new DateOnly(2026, 2, 2), null));
        }
        // Programa demo global (sin tenant), estilo del papel real de CAISY:
        // vacunas y manejos por igual (spec SP7).
        if (!await db.ProgramasVacunacion.IgnoreQueryFilters().AnyAsync(p => p.Id == ProgramaVacunacionDemoId))
        {
            var programa = new ProgramaVacunacion(
                ProgramaVacunacionDemoId, "PROGRAMA DE VACUNACION PARA 1000 AVES (DEMO)",
                new DateOnly(2026, 1, 15), 1000, "Plan de demostración estilo CAISY.");
            programa.ReemplazarCronograma([
                new DatosItemPlanVacunacion(1, "NEWCASTLE + BRONQUITIS", "Gota ocular", null),
                new DatosItemPlanVacunacion(3, "BIO COCCIVET R", "Agua de bebida", null),
                new DatosItemPlanVacunacion(10, "GUMBORO", "Agua de bebida", "Ayuno de agua 2 horas"),
                new DatosItemPlanVacunacion(18, "GUMBORO refuerzo", "Agua de bebida", null),
                new DatosItemPlanVacunacion(30, "Desparasitación", "Agua de bebida", null),
            ]);
            db.ProgramasVacunacion.Add(programa);
        }
        await db.SaveChangesAsync();
    }
}
