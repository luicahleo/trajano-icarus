using Entidad = Icarus.ControlAcceso.Domain.ConfiguracionAccesoTrabajador;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionAccesoTrabajadorEntityTypeConfiguration : IEntityTypeConfiguration<Entidad>
{
    public void Configure(EntityTypeBuilder<Entidad> builder)
    {
        builder.ToTable("configuracion_acceso_trabajador");
        builder.Property(c => c.Enrolamiento).HasConversion<int>();
        builder.HasIndex(c => new { c.ClienteId, c.TrabajadorId }).IsUnique();
    }
}
