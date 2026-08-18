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

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        if (await db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.Id == GranjaDemoId)) return;
        db.Granjas.Add(new Granja(GranjaDemoId, clienteDemoId, "Granja Demo"));
        db.Galpones.Add(new Galpon(GalponDemoNorteId, GranjaDemoId, clienteDemoId, "1", 5000, 4800, new DateOnly(2025, 9, 1), "Galpón norte"));
        db.Galpones.Add(new Galpon(GalponDemoSurId, GranjaDemoId, clienteDemoId, "2", 5000, 5000, new DateOnly(2026, 2, 2), null));
        await db.SaveChangesAsync();
    }
}
