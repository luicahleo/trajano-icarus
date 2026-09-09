using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDetalleDespachoHuevo : IEntityTypeConfiguration<DetalleDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DetalleDespachoHuevo> builder)
    {
        builder.ToTable("detalles_despacho_huevo");
        builder.Property(d => d.Tamano).HasConversion<int>();
        builder.Property(d => d.PrecioProductorCongelado).HasColumnType("decimal(10,4)");

        builder.HasIndex("DespachoHuevoId", nameof(DetalleDespachoHuevo.Tamano)).IsUnique();
    }
}
