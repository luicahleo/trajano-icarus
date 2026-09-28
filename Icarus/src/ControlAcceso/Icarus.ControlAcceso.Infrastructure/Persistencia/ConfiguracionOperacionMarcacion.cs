using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionOperacionMarcacion
    : IEntityTypeConfiguration<OperacionMarcacion>
{
    public void Configure(EntityTypeBuilder<OperacionMarcacion> builder)
    {
        builder.ToTable("operaciones_marcacion");
        builder.Property(o => o.Accion).HasConversion<int>();
        builder.Property(o => o.Estado).HasConversion<int>();
        builder.Property(o => o.Motivo).HasMaxLength(100);
        builder.Property(o => o.CreadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(o => o.ExpiraEnUtc).HasColumnType("datetimeoffset");

        builder.HasIndex(o => new { o.ClienteId, o.ClaveIdempotencia }).IsUnique();
        builder.HasIndex(o => new { o.ClienteId, o.TrabajadorId });
    }
}
