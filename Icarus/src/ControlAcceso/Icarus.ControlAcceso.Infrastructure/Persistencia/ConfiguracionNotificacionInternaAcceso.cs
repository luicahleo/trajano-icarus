using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionNotificacionInternaAcceso
    : IEntityTypeConfiguration<NotificacionInternaAcceso>
{
    public void Configure(EntityTypeBuilder<NotificacionInternaAcceso> builder)
    {
        builder.ToTable("notificaciones_internas_acceso");

        builder.HasIndex(n => new { n.ClienteId, n.FechaUtc });
        builder.HasIndex(n => n.IncidenciaId).IsUnique();
    }
}
