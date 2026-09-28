using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionPlantillaFacial : IEntityTypeConfiguration<PlantillaFacialProtegida>
{
    public void Configure(EntityTypeBuilder<PlantillaFacialProtegida> builder)
    {
        builder.ToTable("plantillas_faciales");
        builder.Property(p => p.ContenidoCifrado).IsRequired();
        builder.Property(p => p.Nonce).IsRequired();
        builder.Property(p => p.Tag).IsRequired();
        builder.Property(p => p.ModeloFormato).HasMaxLength(100).IsRequired();
        builder.Property(p => p.FechaCreacionUtc).HasColumnType("datetimeoffset");
        builder.Property(p => p.FechaRevocacionUtc).HasColumnType("datetimeoffset");

        builder.HasIndex(p => new { p.ClienteId, p.TrabajadorId, p.EstaActivo });
        builder.HasIndex(p => new { p.ClienteId, p.TrabajadorId, p.VersionEnrolamiento }).IsUnique();
    }
}
