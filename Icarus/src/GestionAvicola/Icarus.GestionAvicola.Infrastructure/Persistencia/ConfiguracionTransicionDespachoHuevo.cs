using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionTransicionDespachoHuevo : IEntityTypeConfiguration<TransicionDespachoHuevo>
{
    public void Configure(EntityTypeBuilder<TransicionDespachoHuevo> builder)
    {
        builder.ToTable("transiciones_despacho_huevo");
        builder.Property(t => t.Origen).HasConversion<int>();
        builder.Property(t => t.Destino).HasConversion<int>();
    }
}
