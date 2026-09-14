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

        // Folio legible (spec 2026-09-14): la SEQUENCE de SQL Server asigna el
        // correlativo al insertar. Sin ValueGeneratedOnAdd EF enviaría un 0
        // explícito y la secuencia nunca correría.
        builder.Property(p => p.Numero)
            .HasDefaultValueSql("NEXT VALUE FOR gestion_avicola.secuencia_pedidos_alimento")
            .ValueGeneratedOnAdd();
        builder.HasIndex(p => p.Numero).IsUnique();
        // Índices de los filtros nuevos: sin esto, filtrar por granja sobre una
        // tabla grande es un scan completo, justo lo que este trabajo evita.
        builder.HasIndex(p => new { p.ClienteId, p.GranjaId });
        builder.HasIndex(p => p.CreadoPorTrabajadorId);

        // El conteo del límite semanal filtra por cliente y rango de fechas
        // (spec SP8): el índice respalda la consulta bloqueable del envío.
        builder.HasIndex(p => new { p.ClienteId, p.FechaPedido });

        // La consulta canónica del balance (spec SP8C) filtra por estado
        // recibido, cliente y fecha de pedido.
        builder.HasIndex(p => new { p.Estado, p.ClienteId, p.FechaPedido });

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
