using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionMarcacion : IEntityTypeConfiguration<Marcacion>
{
    public void Configure(EntityTypeBuilder<Marcacion> builder)
    {
        builder.ToTable("marcaciones");
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasConversion<int>();
        builder.Property(m => m.Origen).HasConversion<int>();
        builder.Property(m => m.InstanteUtc).HasColumnType("datetimeoffset");
        builder.Property(m => m.HoraDeclaradaUtc).HasColumnType("datetimeoffset");
        builder.Property(m => m.CreadaEnUtc).HasColumnType("datetimeoffset");
        builder.Property(m => m.Motivo).HasMaxLength(500);
        builder.HasIndex(m => new { m.JornadaAccesoId, m.ClaveIdempotencia }).IsUnique();
    }
}
