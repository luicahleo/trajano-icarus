using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionSesionKiosco : IEntityTypeConfiguration<SesionKiosco>
{
    public void Configure(EntityTypeBuilder<SesionKiosco> builder)
    {
        builder.ToTable("sesiones_kiosco");
        builder.Property(s => s.HashCredencial).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CreadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(s => s.ExpiraEnUtc).HasColumnType("datetimeoffset");
        builder.Property(s => s.RevocadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(s => s.Version).ValueGeneratedNever().IsConcurrencyToken();

        builder.HasIndex(s => s.HashCredencial).IsUnique();
        builder.HasIndex(s => new { s.ClienteId, s.EstaActiva });
    }
}
