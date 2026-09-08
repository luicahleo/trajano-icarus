using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

// Respaldo privado de la nota que aporta el receptor al confirmar la
// recepción (spec SP8D): SQL guarda clave lógica, MIME, tamaños, hash y
// nombre seguro; nunca ruta física, Base64 ni URL pública. El contenido vive
// en el volumen privado.
public sealed class ConfiguracionDocumentoNotaEntrega
    : IEntityTypeConfiguration<DocumentoNotaEntrega>
{
    public void Configure(EntityTypeBuilder<DocumentoNotaEntrega> builder)
    {
        builder.ToTable("documentos_nota_entrega");
        builder.Property(d => d.Mime).HasMaxLength(50).IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.NombreSeguro).HasMaxLength(200).IsRequired();

        builder.HasOne<EntregaPedidoAlimento>().WithMany(e => e.Documentos)
            .HasForeignKey("EntregaPedidoAlimentoId").IsRequired();

        builder.HasIndex("EntregaPedidoAlimentoId");
    }
}
