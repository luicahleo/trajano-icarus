using Icarus.Clientes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.Clientes.Infrastructure.Persistencia;

public sealed class ConfiguracionCliente : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");
        builder.Property(c => c.RazonSocial).HasMaxLength(200).IsRequired();
        builder.Property(c => c.IdentificadorFiscal).HasMaxLength(32).IsRequired();
        builder.HasIndex(c => c.IdentificadorFiscal).IsUnique();
        builder.Property(c => c.ModulosHabilitados).HasConversion<int>();
    }
}
