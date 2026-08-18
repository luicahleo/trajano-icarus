using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionGranja : IEntityTypeConfiguration<Granja>
{
    public void Configure(EntityTypeBuilder<Granja> builder)
    {
        builder.ToTable("granjas");
        builder.Property(g => g.Nombre).HasMaxLength(200).IsRequired();
        builder.HasIndex(g => new { g.ClienteId, g.Nombre }).IsUnique();
        builder.HasIndex(g => g.ClienteId).IsUnique().HasFilter("[EstaActivo] = 1");
    }
}
