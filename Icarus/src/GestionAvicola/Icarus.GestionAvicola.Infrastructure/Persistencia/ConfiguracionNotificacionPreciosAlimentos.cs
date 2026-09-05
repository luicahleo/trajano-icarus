using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionNotificacionPreciosAlimentos
    : IEntityTypeConfiguration<NotificacionPreciosAlimentos>
{
    public void Configure(EntityTypeBuilder<NotificacionPreciosAlimentos> builder)
    {
        builder.ToTable("notificaciones_precios_alimentos", t =>
            t.HasCheckConstraint("CK_notificaciones_precios_aportes",
                "[AporteCaisy] > 0 AND [Fondo] > 0 AND [Servicios] > 0"));
        builder.Property(n => n.FechaDocumento).HasColumnType("date");
        builder.Property(n => n.VigenteDesde).HasColumnType("date");
        builder.Property(n => n.AporteCaisy).HasColumnType("decimal(10,2)");
        builder.Property(n => n.Fondo).HasColumnType("decimal(10,2)");
        builder.Property(n => n.Servicios).HasColumnType("decimal(10,2)");
        builder.Property(n => n.Estado).HasConversion<int>();
        builder.Property(n => n.Version).IsRowVersion();

        // Dos publicaciones activas no comparten vigencia (spec SP8). El
        // filtro usa el valor persistido de Estado: Publicada = 1.
        builder.HasIndex(n => n.VigenteDesde).IsUnique()
            .HasFilter("[Estado] = 1 AND [EstaActivo] = 1");

        builder.HasMany(n => n.Detalles).WithOne()
            .HasForeignKey("NotificacionPreciosAlimentosId").IsRequired();
        builder.Navigation(n => n.Detalles).HasField("_detalles");
    }
}
