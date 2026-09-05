using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionTransicionPedidoAlimento
    : IEntityTypeConfiguration<TransicionPedidoAlimento>
{
    public void Configure(EntityTypeBuilder<TransicionPedidoAlimento> builder)
    {
        builder.ToTable("transiciones_pedidos_alimentos");
        builder.Property(t => t.EstadoOrigen).HasConversion<int>();
        builder.Property(t => t.EstadoDestino).HasConversion<int>();
        builder.Property(t => t.Motivo).HasMaxLength(500);
        builder.Property(t => t.FechaEntregaEstimada).HasColumnType("date");
    }
}
