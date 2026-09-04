using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionRecepcionPedidoAlimento
    : IEntityTypeConfiguration<RecepcionPedidoAlimento>
{
    public void Configure(EntityTypeBuilder<RecepcionPedidoAlimento> builder)
    {
        builder.ToTable("recepciones_pedidos_alimentos");
        builder.Property(r => r.FechaRecepcion).HasColumnType("date");
        builder.Property(r => r.TotalRecibido).HasColumnType("decimal(18,2)");
        // Snapshot de diferencias como JSON (patrón Meta): persistido, sin
        // tabla propia; el detalle también vive en memoria para los mapeos.
        builder.Property(r => r.DiferenciasJson).IsRequired();
        builder.Ignore(r => r.Diferencias);

        // Una sola recepción por pedido (spec SP8C): estados terminales.
        builder.HasOne<PedidoAlimento>().WithOne(p => p.Recepcion)
            .HasForeignKey<RecepcionPedidoAlimento>("PedidoAlimentoId").IsRequired();
        builder.HasIndex("PedidoAlimentoId").IsUnique();

        builder.Navigation(r => r.Lineas).HasField("_lineas");
    }
}

public sealed class ConfiguracionDetalleRecepcionPedidoAlimento
    : IEntityTypeConfiguration<DetalleRecepcionPedidoAlimento>
{
    public void Configure(EntityTypeBuilder<DetalleRecepcionPedidoAlimento> builder)
    {
        builder.ToTable("detalles_recepciones_pedidos_alimentos", t =>
        {
            // Sin negativos (spec SP8C); el cero cabe como diferencia extrema.
            t.HasCheckConstraint("CK_detalles_recepciones_cantidad", "[CantidadRecibida] >= 0");
        });
        builder.Property(d => d.TipoAlimento).HasConversion<int>();
        builder.Property(d => d.Presentacion).HasConversion<int>();

        // Relación definida aquí: el config del detalle se aplica antes que el
        // de la recepción (orden alfabético) y la FK shadow debe existir antes
        // de crear el índice único que la usa.
        builder.HasOne<RecepcionPedidoAlimento>().WithMany(r => r.Lineas)
            .HasForeignKey("RecepcionPedidoAlimentoId").IsRequired();

        // Cada tipo aparece una sola vez en el pedido: una sola línea
        // recibida por tipo.
        builder.HasIndex("RecepcionPedidoAlimentoId", nameof(DetalleRecepcionPedidoAlimento.TipoAlimento))
            .IsUnique();
    }
}
