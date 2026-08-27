using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionProgramaVacunacion : IEntityTypeConfiguration<ProgramaVacunacion>
{
    public void Configure(EntityTypeBuilder<ProgramaVacunacion> builder)
    {
        builder.ToTable("programas_vacunacion", t =>
            t.HasCheckConstraint("CK_programas_vacunacion_cantidad_aves", "[CantidadAves] > 0"));
        builder.Property(p => p.Nombre).HasMaxLength(200);
        builder.Property(p => p.Observaciones).HasMaxLength(1000);
        builder.Property(p => p.FechaEmision).HasColumnType("date");
        // Unicidad incluyendo inactivos (spec SP7): el soft delete no libera el nombre.
        builder.HasIndex(p => p.Nombre).IsUnique();
        builder.HasMany(p => p.Items).WithOne().HasForeignKey("ProgramaVacunacionId").IsRequired();
        builder.Navigation(p => p.Items).HasField("_items");
    }
}
