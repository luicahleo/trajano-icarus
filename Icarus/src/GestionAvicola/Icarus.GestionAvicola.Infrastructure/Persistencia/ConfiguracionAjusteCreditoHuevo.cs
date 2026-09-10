using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionAjusteCreditoHuevo : IEntityTypeConfiguration<AjusteCreditoHuevo>
{
    public void Configure(EntityTypeBuilder<AjusteCreditoHuevo> builder)
    {
        builder.ToTable("ajustes_credito_huevo", t =>
            t.HasCheckConstraint("CK_ajustes_credito_huevo_monto", "[Monto] <> 0"));
        // Mismo decimal(18,2) que otros totales agregados del módulo
        // (PedidoAlimento.Recepcion.TotalRecibido, DetallePedidoAlimento.SubtotalSolicitado).
        builder.Property(a => a.Monto).HasColumnType("decimal(18,2)");
        builder.Property(a => a.Motivo).HasMaxLength(500).IsRequired();

        builder.HasIndex(a => a.ClienteId);
        builder.HasIndex(a => a.DespachoHuevoId);
    }
}
