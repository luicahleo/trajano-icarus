using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionEntregaPedidoAlimento
    : IEntityTypeConfiguration<EntregaPedidoAlimento>
{
    public void Configure(EntityTypeBuilder<EntregaPedidoAlimento> builder)
    {
        builder.ToTable("entregas_pedidos_alimentos");
        builder.Property(e => e.NumeroNota).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FechaNota).HasColumnType("date");
        builder.Property(e => e.FechaDespacho).HasColumnType("date");
        builder.Property(e => e.TotalNetoInformado).HasColumnType("decimal(18,2)");

        // Una sola entrega por pedido (spec SP8C): índice único de la
        // relación uno a uno con el agregado.
        builder.HasOne<PedidoAlimento>().WithOne(p => p.Entrega)
            .HasForeignKey<EntregaPedidoAlimento>("PedidoAlimentoId").IsRequired();
        builder.HasIndex("PedidoAlimentoId").IsUnique();

        builder.Navigation(e => e.Lineas).HasField("_lineas");
        builder.Navigation(e => e.Documentos).HasField("_documentos");
    }
}

public sealed class ConfiguracionDetalleEntregaPedidoAlimento
    : IEntityTypeConfiguration<DetalleEntregaPedidoAlimento>
{
    public void Configure(EntityTypeBuilder<DetalleEntregaPedidoAlimento> builder)
    {
        builder.ToTable("detalles_entregas_pedidos_alimentos", t =>
        {
            // Sin negativos (spec SP8C); el cero se admite como diferencia
            // extrema contra lo solicitado.
            t.HasCheckConstraint("CK_detalles_entregas_cantidad", "[CantidadEntregada] >= 0");
        });
        builder.Property(d => d.TipoAlimento).HasConversion<int>();
        builder.Property(d => d.Presentacion).HasConversion<int>();

        // Relación definida aquí: el config del detalle se aplica antes que el
        // de la entrega (orden alfabético) y la FK shadow debe existir antes
        // de crear el índice único que la usa.
        builder.HasOne<EntregaPedidoAlimento>().WithMany(e => e.Lineas)
            .HasForeignKey("EntregaPedidoAlimentoId").IsRequired();

        // Cada tipo aparece una sola vez en el pedido: una sola línea
        // entregada por tipo.
        builder.HasIndex("EntregaPedidoAlimentoId", nameof(DetalleEntregaPedidoAlimento.TipoAlimento))
            .IsUnique();
    }
}
