using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionDetallePedidoAlimento
    : IEntityTypeConfiguration<DetallePedidoAlimento>
{
    public void Configure(EntityTypeBuilder<DetallePedidoAlimento> builder)
    {
        builder.ToTable("detalles_pedidos_alimentos", t =>
        {
            t.HasCheckConstraint("CK_detalles_pedidos_cantidad", "[CantidadSolicitada] > 0");
            // El congelado al enviar (spec SP8) exige precio positivo: la
            // restricción cubre solo las líneas con precio (borradores sin
            // congelar guardan NULL).
            t.HasCheckConstraint("CK_detalles_pedidos_precio",
                "[PrecioFinalPor40Kg] IS NULL OR [PrecioFinalPor40Kg] > 0");
        });
        builder.Property(d => d.TipoAlimento).HasConversion<int>();
        builder.Property(d => d.Presentacion).HasConversion<int>();
        builder.Property(d => d.PrecioFinalPor40Kg).HasColumnType("decimal(10,2)");
        builder.Property(d => d.SubtotalSolicitado).HasColumnType("decimal(18,2)");

        // Un pedido admite una sola línea por tipo (spec SP8).
        builder.HasIndex("PedidoAlimentoId", nameof(DetallePedidoAlimento.TipoAlimento))
            .IsUnique();
    }
}
