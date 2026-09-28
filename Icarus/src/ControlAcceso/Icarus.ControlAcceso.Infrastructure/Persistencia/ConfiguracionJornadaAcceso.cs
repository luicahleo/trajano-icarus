using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionJornadaAcceso : IEntityTypeConfiguration<JornadaAcceso>
{
    public void Configure(EntityTypeBuilder<JornadaAcceso> builder)
    {
        builder.ToTable("jornadas_acceso");
        builder.Property(j => j.FechaBoliviana).HasColumnType("date");
        builder.Property(j => j.Version).ValueGeneratedNever().IsConcurrencyToken();
        builder.HasIndex(j => new { j.ClienteId, j.TrabajadorId, j.FechaBoliviana }).IsUnique();

        builder.HasMany(j => j.Marcaciones).WithOne()
            .HasForeignKey("JornadaAccesoId").IsRequired();
        builder.Navigation(j => j.Marcaciones).HasField("_marcaciones");

        builder.HasMany(j => j.Revisiones).WithOne()
            .HasForeignKey("JornadaAccesoId").IsRequired();
        builder.Navigation(j => j.Revisiones).HasField("_revisiones");
    }
}
