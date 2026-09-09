using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDocumentoDespachoHuevo : IEntityTypeConfiguration<DocumentoDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<DocumentoDespachoHuevo> builder)
    {
        builder.ToTable("documentos_despacho_huevo");
        builder.Property(d => d.Mime).HasMaxLength(100);
        builder.Property(d => d.HashSha256).HasMaxLength(64);
        builder.Property(d => d.NombreSeguro).HasMaxLength(200);

        builder.HasIndex("DespachoHuevoId").IsUnique();
    }
}
