using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionPedidoAlimento : IEntityTypeConfiguration<PedidoAlimento>
{
    public void Configure(EntityTypeBuilder<PedidoAlimento> builder)
    {
        builder.ToTable("pedidos_alimentos");
        builder.Property(p => p.FechaPedido).HasColumnType("date");
        builder.Property(p => p.FechaEntregaEstimada).HasColumnType("date");
        builder.Property(p => p.Estado).HasConversion<int>();
        builder.Property(p => p.Version).IsRowVersion();

        // El conteo del límite semanal filtra por cliente y rango de fechas
        // (spec SP8): el índice respalda la consulta bloqueable del envío.
        builder.HasIndex(p => new { p.ClienteId, p.FechaPedido });

        builder.HasMany(p => p.Detalles).WithOne()
            .HasForeignKey("PedidoAlimentoId").IsRequired();
        builder.Navigation(p => p.Detalles).HasField("_detalles");
        builder.HasMany(p => p.Historial).WithOne()
            .HasForeignKey("PedidoAlimentoId").IsRequired();
        builder.Navigation(p => p.Historial).HasField("_historial");
        builder.HasOne(p => p.Entrega).WithOne()
            .HasForeignKey<EntregaPedidoAlimento>("PedidoAlimentoId").IsRequired();
        builder.Navigation(p => p.Entrega).HasField("_entrega");
        builder.HasOne(p => p.Recepcion).WithOne()
            .HasForeignKey<RecepcionPedidoAlimento>("PedidoAlimentoId").IsRequired();
        builder.Navigation(p => p.Recepcion).HasField("_recepcion");
    }
}
