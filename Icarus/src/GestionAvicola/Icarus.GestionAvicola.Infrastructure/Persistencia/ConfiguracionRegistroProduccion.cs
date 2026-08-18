using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Icarus.GestionAvicola.Infrastructure.Persistencia;
public sealed class ConfiguracionRegistroProduccion : IEntityTypeConfiguration<RegistroProduccion>
{ public void Configure(EntityTypeBuilder<RegistroProduccion> b) { b.ToTable("registros_produccion", t => { t.HasCheckConstraint("CK_registros_produccion_maples", "[CantidadMaples] >= 0 AND [MaplesDescarte] >= 0"); t.HasCheckConstraint("CK_registros_produccion_sueltos", "[UnidadesIncompletas] >= 0 AND [UnidadesIncompletas] < 30 AND [UnidadesDescarte] >= 0 AND [UnidadesDescarte] < 30"); }); b.Property(x => x.Fecha).HasColumnType("date"); b.Property(x => x.Hora).HasColumnType("time"); b.HasIndex(x => new { x.GalponId, x.Fecha }); b.HasIndex(x => x.ClienteId); b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL"); } }
