using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionFlujoMarcacion
    : IEntityTypeConfiguration<FlujoMarcacion>
{
    public void Configure(EntityTypeBuilder<FlujoMarcacion> builder)
    {
        builder.ToTable("flujos_marcacion");
        builder.Property(f => f.Accion).HasConversion<int>();
        builder.Property(f => f.Estado).HasConversion<int>();
        builder.Property(f => f.CreadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(f => f.FinalizadaEnUtc).HasColumnType("datetimeoffset");

        builder.HasMany(f => f.Capturas)
            .WithOne()
            .HasForeignKey("FlujoMarcacionId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Capturas).HasField("_capturas");

        builder.HasIndex(f => new { f.ClienteId, f.SesionKioscoId, f.Estado });
        builder.HasIndex(f => f.ClienteId);
    }
}
