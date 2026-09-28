using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ConfiguracionRevisionJornada : IEntityTypeConfiguration<RevisionJornada>
{
    public void Configure(EntityTypeBuilder<RevisionJornada> builder)
    {
        builder.ToTable("revisiones_jornada");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.InstanteCorreccionUtc).HasColumnType("datetimeoffset");
        builder.Property(r => r.Motivo).HasMaxLength(500).IsRequired();

        builder.OwnsMany(r => r.ValoresEfectivos, valores =>
        {
            valores.ToTable("revisiones_jornada_valores");
            valores.Property(v => v.Tipo).HasConversion<int>();
            valores.Property(v => v.InstanteUtc).HasColumnType("datetimeoffset");
            valores.WithOwner().HasForeignKey("RevisionJornadaId");
        });
    }
}
