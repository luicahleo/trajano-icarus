using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Icarus.GestionAvicola.Infrastructure.Persistencia;
public sealed class ConfiguracionRegistroMortalidad : IEntityTypeConfiguration<RegistroMortalidad>
{ public void Configure(EntityTypeBuilder<RegistroMortalidad> b) { b.ToTable("registros_mortalidad", t => t.HasCheckConstraint("CK_registros_mortalidad_cantidad", "[CantidadMuertas] > 0")); b.Property(x => x.Fecha).HasColumnType("date"); b.Property(x => x.Hora).HasColumnType("time"); b.HasIndex(x => new { x.GalponId, x.Fecha }); b.HasIndex(x => x.ClienteId); b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL"); } }
