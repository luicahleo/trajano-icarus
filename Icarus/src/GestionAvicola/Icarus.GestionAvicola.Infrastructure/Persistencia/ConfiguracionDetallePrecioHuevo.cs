using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDetallePrecioHuevo : IEntityTypeConfiguration<DetallePrecioHuevo>
{
    public void Configure(EntityTypeBuilder<DetallePrecioHuevo> builder)
    {
        builder.ToTable("detalles_precio_huevo", t =>
            t.HasCheckConstraint("CK_detalles_precio_huevo_productor", "[PrecioAlProductor] > 0"));
        builder.Property(d => d.Tamano).HasConversion<int>();
        builder.Property(d => d.PrecioAlProductor).HasColumnType("decimal(10,4)");
        builder.Property(d => d.PrecioActualDocumento).HasColumnType("decimal(10,4)");

        // Un solo detalle por (publicación, tamaño) (spec SP9).
        builder.HasIndex("PublicacionPrecioHuevoId", nameof(DetallePrecioHuevo.Tamano)).IsUnique();
    }
}
