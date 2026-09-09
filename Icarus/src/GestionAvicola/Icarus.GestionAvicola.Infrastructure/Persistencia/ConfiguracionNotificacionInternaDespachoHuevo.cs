using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionNotificacionInternaDespachoHuevo
    : IEntityTypeConfiguration<NotificacionInternaDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<NotificacionInternaDespachoHuevo> builder)
    {
        builder.ToTable("notificaciones_internas_despacho_huevo");
        builder.Property(n => n.Tipo).HasConversion<int>();
        builder.Property(n => n.Meta).HasMaxLength(500);
        builder.HasIndex(n => new { n.ClienteId, n.FechaUtc });
        builder.HasIndex(n => n.DespachoHuevoId);
    }
}
