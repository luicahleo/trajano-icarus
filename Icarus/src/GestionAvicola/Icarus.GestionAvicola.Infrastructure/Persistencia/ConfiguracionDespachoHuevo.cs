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

        // Folio legible (spec 2026-09-14): misma SEQUENCE por tipo que los
        // pedidos, con su propia serie para los despachos.
        builder.Property(d => d.Numero)
            .HasDefaultValueSql("NEXT VALUE FOR gestion_avicola.secuencia_despachos_huevo")
            .ValueGeneratedOnAdd();
        builder.HasIndex(d => d.Numero).IsUnique();
        builder.HasIndex(d => new { d.ClienteId, d.GranjaId });
        builder.HasIndex(d => d.CreadoPorTrabajadorId);

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
