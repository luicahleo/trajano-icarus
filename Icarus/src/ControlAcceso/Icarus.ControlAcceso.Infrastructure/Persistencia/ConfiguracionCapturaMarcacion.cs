using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionCapturaMarcacion
    : IEntityTypeConfiguration<CapturaMarcacion>
{
    public void Configure(EntityTypeBuilder<CapturaMarcacion> builder)
    {
        builder.ToTable("capturas_marcacion");
        builder.Property(c => c.Resultado).HasConversion<int>();
        builder.Property(c => c.Motivo).HasMaxLength(100);
        builder.Property(c => c.InstanteUtc).HasColumnType("datetimeoffset");

        builder.HasIndex(c => new { c.ClaveCaptura }).IsUnique();
        builder.HasIndex("FlujoMarcacionId", "ClaveCaptura").IsUnique();
    }
}
