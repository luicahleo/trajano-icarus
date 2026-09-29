using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionIncidenciaAcceso
    : IEntityTypeConfiguration<IncidenciaAcceso>
{
    public void Configure(EntityTypeBuilder<IncidenciaAcceso> builder)
    {
        builder.ToTable("incidencias_acceso");
        builder.Property(i => i.Accion).HasConversion<int>();
        builder.Property(i => i.Estado).HasConversion<int>();
        builder.Property(i => i.PrimerRechazoUtc).HasColumnType("datetimeoffset");
        builder.Property(i => i.TercerRechazoUtc).HasColumnType("datetimeoffset");
        builder.Property(i => i.CreadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(i => i.ResueltaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(i => i.MotivoResolucion).HasMaxLength(500);

        builder.HasIndex(i => new { i.ClienteId, i.Estado, i.TercerRechazoUtc });
        builder.HasIndex(i => i.FlujoMarcacionId).IsUnique();
    }
}
