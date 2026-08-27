using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionItemPlanVacunacion : IEntityTypeConfiguration<ItemPlanVacunacion>
{
    public void Configure(EntityTypeBuilder<ItemPlanVacunacion> builder)
    {
        builder.ToTable("programas_vacunacion_items", t =>
            t.HasCheckConstraint("CK_programas_vacunacion_items_edad", "[EdadDia] > 0"));
        builder.Property(i => i.Vacuna).HasMaxLength(200);
        builder.Property(i => i.ModoAplicacion).HasMaxLength(500);
        builder.Property(i => i.Observaciones).HasMaxLength(1000);
        builder.HasIndex("ProgramaVacunacionId");
    }
}
