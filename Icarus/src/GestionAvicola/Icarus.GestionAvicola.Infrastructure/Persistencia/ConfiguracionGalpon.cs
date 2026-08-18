using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionGalpon : IEntityTypeConfiguration<Galpon>
{
    public void Configure(EntityTypeBuilder<Galpon> builder)
    {
        builder.ToTable("galpones", t =>
        {
            t.HasCheckConstraint("CK_galpones_capacidad", "[CapacidadMaxima] > 0");
            t.HasCheckConstraint("CK_galpones_inventario", "[GallinasActuales] >= 0 AND [GallinasActuales] <= [CapacidadMaxima]");
        });
        builder.Property(g => g.Numero).HasMaxLength(10).IsRequired();
        builder.Property(g => g.Descripcion).HasMaxLength(500);
        builder.HasIndex(g => new { g.GranjaId, g.Numero }).IsUnique();
        builder.HasIndex(g => g.ClienteId);
    }
}
