using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDetallePrecioAlimento : IEntityTypeConfiguration<DetallePrecioAlimento>
{
    public void Configure(EntityTypeBuilder<DetallePrecioAlimento> builder)
    {
        builder.ToTable("detalles_precio_alimento", t =>
            t.HasCheckConstraint("CK_detalles_precio_final", "[PrecioFinalPor40Kg] > 0"));
        builder.Property(d => d.TipoAlimento).HasConversion<int>();
        builder.Property(d => d.Presentacion).HasConversion<int>();
        builder.Property(d => d.PrecioFinalPor40Kg).HasColumnType("decimal(10,2)");
        builder.Property(d => d.PrecioActualDocumento).HasColumnType("decimal(10,2)");

        // La identidad del producto no incluye la presentación (spec SP8):
        // un solo detalle por (tipo, presentación) dentro de cada notificación.
        builder.HasIndex(
            "NotificacionPreciosAlimentosId",
            nameof(DetallePrecioAlimento.TipoAlimento),
            nameof(DetallePrecioAlimento.Presentacion)).IsUnique();
    }
}
