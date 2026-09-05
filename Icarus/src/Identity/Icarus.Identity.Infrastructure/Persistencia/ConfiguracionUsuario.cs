using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.Identity.Infrastructure.Persistencia;

public sealed class ConfiguracionUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.Property(u => u.Rol).HasMaxLength(32).IsRequired();
        builder.Property(u => u.FuncionalidadesCaisy).HasConversion<int>();
    }
}
