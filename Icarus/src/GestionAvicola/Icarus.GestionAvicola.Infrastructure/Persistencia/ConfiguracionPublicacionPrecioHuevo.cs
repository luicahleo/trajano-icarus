using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionPublicacionPrecioHuevo
    : IEntityTypeConfiguration<PublicacionPrecioHuevo>
{
    public void Configure(EntityTypeBuilder<PublicacionPrecioHuevo> builder)
    {
        builder.ToTable("publicaciones_precios_huevo", t =>
            t.HasCheckConstraint("CK_publicaciones_precios_huevo_servicio", "[Servicio] > 0"));
        builder.Property(p => p.FechaNotificacion).HasColumnType("date");
        builder.Property(p => p.FechaVigencia).HasColumnType("date");
        // El precio real de CAISY llega con 4 decimales (0.7957), por eso
        // decimal(10,4) en vez de decimal(10,2) como en alimento.
        builder.Property(p => p.Servicio).HasColumnType("decimal(10,4)");
        builder.Property(p => p.Estado).HasConversion<int>();
        builder.Property(p => p.Version).IsRowVersion();

        // Dos publicaciones activas no comparten vigencia (spec SP9). El
        // filtro usa el valor persistido de Estado: Publicada = 1.
        builder.HasIndex(p => p.FechaVigencia).IsUnique()
            .HasFilter("[Estado] = 1 AND [EstaActivo] = 1");

        builder.HasMany(p => p.Detalles).WithOne()
            .HasForeignKey("PublicacionPrecioHuevoId").IsRequired();
        builder.Navigation(p => p.Detalles).HasField("_detalles");
    }
}
