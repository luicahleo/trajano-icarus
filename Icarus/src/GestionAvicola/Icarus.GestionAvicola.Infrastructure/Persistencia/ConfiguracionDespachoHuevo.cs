using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDespachoHuevo : IEntityTypeConfiguration<DespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DespachoHuevo> builder)
    {
        builder.ToTable("despachos_huevo");
        builder.Property(d => d.FechaDespacho).HasColumnType("date");
        builder.Property(d => d.FechaRecepcion).HasColumnType("date");
        builder.Property(d => d.Estado).HasConversion<int>();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasIndex(d => new { d.ClienteId, d.FechaDespacho });
        builder.HasIndex(d => new { d.Estado, d.ClienteId, d.FechaDespacho });

        builder.HasMany(d => d.Detalles).WithOne()
            .HasForeignKey("DespachoHuevoId").IsRequired();
        builder.Navigation(d => d.Detalles).HasField("_detalles");
        builder.HasMany(d => d.Historial).WithOne()
            .HasForeignKey("DespachoHuevoId").IsRequired();
        builder.Navigation(d => d.Historial).HasField("_historial");
        builder.HasOne(d => d.DocumentoNota).WithOne()
            .HasForeignKey<DocumentoDespachoHuevo>("DespachoHuevoId");
        builder.Navigation(d => d.DocumentoNota).HasField("_documentoNota");
    }
}
