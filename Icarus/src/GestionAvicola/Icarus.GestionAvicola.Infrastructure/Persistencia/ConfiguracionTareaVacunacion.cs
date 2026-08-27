using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionTareaVacunacion : IEntityTypeConfiguration<TareaVacunacion>
{
    public void Configure(EntityTypeBuilder<TareaVacunacion> builder)
    {
        builder.ToTable("tareas_vacunacion", t =>
        {
            // Las invariantes del agregado, como última línea de defensa en BD.
            t.HasCheckConstraint("CK_tareas_vacunacion_edad", "[EdadDia] > 0");
            t.HasCheckConstraint("CK_tareas_vacunacion_aves", "[AvesVacunadas] IS NULL OR [AvesVacunadas] > 0");
            t.HasCheckConstraint("CK_tareas_vacunacion_estado_fecha",
                "[Estado] <> 'Completada' OR [FechaAplicacion] IS NOT NULL");
        });
        builder.Property(t => t.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Vacuna).HasMaxLength(200);
        builder.Property(t => t.ModoAplicacion).HasMaxLength(500);
        builder.Property(t => t.ObservacionesProgramadas).HasMaxLength(1000);
        builder.Property(t => t.ObservacionesAplicacion).HasMaxLength(1000);
        builder.Property(t => t.MotivoCancelacion).HasMaxLength(500);
        builder.Property(t => t.FechaProgramada).HasColumnType("date");
        builder.Property(t => t.FechaAplicacion).HasColumnType("date");
        builder.HasIndex(t => new { t.ClienteId, t.FechaProgramada });
        builder.HasIndex(t => new { t.GalponId, t.Estado });
    }
}
