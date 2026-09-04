using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionNotificacionInterna
    : IEntityTypeConfiguration<NotificacionInterna>
{
    public void Configure(EntityTypeBuilder<NotificacionInterna> builder)
    {
        builder.ToTable("notificaciones_internas");
        builder.Property(n => n.Tipo).HasConversion<int>();
        builder.Property(n => n.Meta).HasMaxLength(500);

        // El alcance va explícito en cada consulta del repositorio (null =
        // bandeja global de CAISY): el índice respalda ambos recorridos.
        builder.HasIndex(n => new { n.ClienteId, n.FechaUtc });
        builder.HasIndex(n => n.PedidoId);
    }
}
