using Icarus.Clientes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.Clientes.Infrastructure.Persistencia;

public sealed class ConfiguracionTrabajador : IEntityTypeConfiguration<Trabajador>
{
    public void Configure(EntityTypeBuilder<Trabajador> builder)
    {
        builder.ToTable("trabajadores");
        builder.Property(t => t.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(t => t.DocumentoIdentidad).HasMaxLength(32).IsRequired();
        builder.Property(t => t.Cargo).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Funcionalidades).HasConversion<int>().HasDefaultValue(Funcionalidades.Ninguno);

        // Documento único por cliente (spec), también contra trabajadores
        // desactivados: el soft delete no libera el documento (trazabilidad).
        builder.HasIndex(t => new { t.ClienteId, t.DocumentoIdentidad }).IsUnique();
    }
}
